using Autodesk.Revit.DB;

namespace FERCPlugin.Core.Models
{
    public class VentUnitGeometryBuilder
    {
        private readonly Document _doc;
        private readonly List<VentUnitItem> _intakeUnits;
        private readonly List<VentUnitItem> _exhaustUnits;
        private readonly bool _isIntakeBelow;
        private readonly double _frameHeight;

        private double _maxHeightIntake;
        private double _maxHeightExhaust;
        private double _maxWidth;
        private double _totalLengthIntake;
        private double _totalLengthExhaust;

        private const double MM_TO_FEET = 0.00328084;

        public VentUnitGeometryBuilder(Document doc, List<VentUnitItem> intakeUnits, List<VentUnitItem> exhaustUnits, bool isIntakeBelow, double frameHeight)
        {
            _doc = doc;
            _intakeUnits = intakeUnits;
            _exhaustUnits = exhaustUnits;
            _isIntakeBelow = isIntakeBelow;
            _frameHeight = frameHeight;

            if (_intakeUnits.Count > 0)
            {
                _maxHeightIntake = GetMaxHeight(_intakeUnits);
                _totalLengthIntake = GetTotalLength(_intakeUnits);
            }
            if (_exhaustUnits.Count > 0)
            {
                _maxHeightExhaust = GetMaxHeight(_exhaustUnits);
                _totalLengthExhaust = GetTotalLength(_exhaustUnits);
            }
            _maxWidth = GetMaxWidth(_intakeUnits, _exhaustUnits);
        }

        private static double GetMaxHeight(List<VentUnitItem> units) =>
    units.Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
         .Max(unit => unit.HeightTotal) * MM_TO_FEET;

        private static double GetMaxWidth(List<VentUnitItem> intakeUnits, List<VentUnitItem> exhaustUnits)
        {
            double maxWidthIntake = intakeUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
                .Select(unit => unit.WidthTotal)
                .DefaultIfEmpty(0)
                .Max() * MM_TO_FEET;

            double maxWidthExhaust = exhaustUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
                .Select(unit => unit.WidthTotal)
                .DefaultIfEmpty(0)
                .Max() * MM_TO_FEET;

            return Math.Max(maxWidthIntake, maxWidthExhaust);
        }

        private static double GetTotalLength(List<VentUnitItem> units) =>
            units.Sum(unit => unit.LengthTotal) * MM_TO_FEET;

        public (List<Tuple<Element, VentUnitItem>>, List<Tuple<Element, VentUnitItem>>, double, double) BuildGeometry()
        {
            List<Tuple<Element, VentUnitItem>> intakeElements = new();
            List<Tuple<Element, VentUnitItem>> exhaustElements = new();

            using (Transaction tx = new Transaction(_doc, "Build Vent Unit Geometry"))
            {
                tx.Start();

                Dictionary<string, double> intakePositions = new();

                if (_intakeUnits.Count > 0)
                {
                    intakePositions = _isIntakeBelow
                                    ? CreateIntakeGeometry(intakeElements, -_maxHeightIntake / 2)
                                    : CreateIntakeGeometry(intakeElements, _maxHeightExhaust);
                }

                if (_exhaustUnits.Count > 0)
                {
                    CreateExhaustGeometry(intakePositions, exhaustElements, intakeElements);
                }

                tx.Commit();
            }

            return (intakeElements, exhaustElements, _maxHeightIntake, _maxHeightExhaust);
        }

        private Dictionary<string, double> CreateIntakeGeometry(List<Tuple<Element, VentUnitItem>> intakeElements, double intakeBaseZ)
        {
            double currentX = -_totalLengthIntake / 2;
            Dictionary<string, double> intakePositions = new();

            var utilizer = _intakeUnits.FirstOrDefault(unit => unit.Id.Contains("plateUtilizer"));

            if (utilizer != null)
            {
                List<VentUnitItem> leftElements = new();
                List<VentUnitItem> rightElements = new();

                if (utilizer != null)
                {
                    int utilizerIndex = _intakeUnits.IndexOf(utilizer);
                    leftElements = _intakeUnits.Take(utilizerIndex).ToList();
                    rightElements = _intakeUnits.Skip(utilizerIndex + 1).ToList();
                }

                bool hasLeftCut = false, hasRightCut = false;
                double cutSize = 0;

                if (utilizer != null)
                {
                    (hasLeftCut, hasRightCut, cutSize) = GetPlateUtilizerCutInfo(utilizer);
                }

                if (!_isIntakeBelow && hasLeftCut)
                {
                    currentX += cutSize;
                }

                foreach (var unit in leftElements)
                {
                    double elementBaseZ = intakeBaseZ;

                    if (!_isIntakeBelow && unit.HeightTotal * MM_TO_FEET > _maxHeightIntake / 2)
                    {
                        elementBaseZ -= unit.HeightTotal * MM_TO_FEET - _maxHeightIntake / 2;
                    }

                    Element createdElement = CreateIntakeExtrusion(unit, currentX, elementBaseZ);

                    if (createdElement != null)
                    {
                        intakeElements.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
                    }

                    foreach (var child in unit.Children)
                    {
                        if (!intakePositions.ContainsKey(child.Id))
                            intakePositions[child.Id] = currentX;
                    }

                    currentX += unit.LengthTotal * MM_TO_FEET;
                }

                if (!_isIntakeBelow && hasLeftCut)
                {
                    currentX -= cutSize;
                }

                if (utilizer != null)
                {
                    double elementBaseZ = intakeBaseZ;

                    if (!_isIntakeBelow && utilizer.HeightTotal * MM_TO_FEET > _maxHeightIntake / 2)
                    {
                        elementBaseZ -= utilizer.HeightTotal * MM_TO_FEET - _maxHeightIntake / 2;
                    }

                    Element createdElement = CreateIntakeExtrusion(utilizer, currentX, elementBaseZ);

                    if (createdElement != null)
                    {
                        intakeElements.Add(new Tuple<Element, VentUnitItem>(createdElement, utilizer));
                    }

                    foreach (var child in utilizer.Children)
                    {
                        if (!intakePositions.ContainsKey(child.Id))
                            intakePositions[child.Id] = currentX;
                    }

                    currentX += utilizer.LengthTotal * MM_TO_FEET;
                }

                if (!_isIntakeBelow && hasRightCut)
                {
                    currentX -= cutSize;
                }

                foreach (var unit in rightElements)
                {
                    double elementBaseZ = intakeBaseZ;

                    if (!_isIntakeBelow && unit.HeightTotal * MM_TO_FEET > _maxHeightIntake / 2)
                    {
                        elementBaseZ -= unit.HeightTotal * MM_TO_FEET - _maxHeightIntake / 2;
                    }

                    Element createdElement = CreateIntakeExtrusion(unit, currentX, elementBaseZ);

                    if (createdElement != null)
                    {
                        intakeElements.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
                    }

                    foreach (var child in unit.Children)
                    {
                        if (!intakePositions.ContainsKey(child.Id))
                            intakePositions[child.Id] = currentX;
                    }

                    currentX += unit.LengthTotal * MM_TO_FEET;
                }
            }

            if (utilizer == null)
            {
                foreach (var unit in _intakeUnits)
                {
                    double elementBaseZ = intakeBaseZ;

                    if (!_isIntakeBelow && unit.HeightTotal * MM_TO_FEET > _maxHeightIntake / 2)
                    {
                        elementBaseZ -= unit.HeightTotal * MM_TO_FEET - _maxHeightIntake / 2;
                    }

                    Element createdElement = CreateIntakeExtrusion(unit, currentX, elementBaseZ);

                    if (createdElement != null)
                    {
                        intakeElements.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
                    }

                    foreach (var child in unit.Children)
                    {
                        if (!intakePositions.ContainsKey(child.Id))
                            intakePositions[child.Id] = currentX;
                    }

                    currentX += unit.LengthTotal * MM_TO_FEET;
                }
            }

            return intakePositions;
        }

        private void CreateExhaustGeometry(Dictionary<string, double> intakePositions, List<Tuple<Element, VentUnitItem>> exhaustElements, List<Tuple<Element, VentUnitItem>> intakeElements)
        {
            double currentX = -_totalLengthExhaust / 2;
            double exhaustBaseZ = _isIntakeBelow ? _maxHeightIntake / 2 : -_maxHeightExhaust / 2;
            List<VentUnitItem> commonElements = new();
            List<VentUnitItem> exhaustLeft = new();
            List<VentUnitItem> exhaustRight = new();

            double referenceX = 0;
            double totalCommonLength = 0;
            int commonIndex = -1;

            for (int i = 0; i < _exhaustUnits.Count; i++)
            {
                var unit = _exhaustUnits[i];
                bool isCommonElement = false;

                foreach (var child in unit.Children)
                {
                    if (intakePositions.TryGetValue(child.Id, out double intakeX))
                    {
                        if (commonElements.Count == 0)
                        {
                            referenceX = intakeX;
                            commonIndex = i;
                        }

                        var matchingIntakeElement = intakeElements.FirstOrDefault(e => e.Item2.Id.Split('-').First() == unit.Id.Split('-').First());

                        if (matchingIntakeElement != null)
                        {
                            totalCommonLength += matchingIntakeElement.Item2.LengthTotal * MM_TO_FEET;
                        }
                        else
                        {
                            totalCommonLength += unit.LengthTotal * MM_TO_FEET;
                        }

                        commonElements.Add(unit);
                        isCommonElement = true;
                    }
                }

                if (!isCommonElement)
                {
                    if (commonIndex == -1)
                        exhaustLeft.Add(unit);
                    else
                        exhaustRight.Add(unit);
                }
            }
            double offsetLeft = 0;
            double offsetRight = 0;

            if (_isIntakeBelow && intakeElements.Count > 0)
            {
                offsetLeft = intakeElements.Any(e => e.Item2.CutInfo.HasLeftCut) ? intakeElements.Max(e => e.Item2.CutInfo.CutSize) : 0;
                offsetRight = intakeElements.Any(e => e.Item2.CutInfo.HasRightCut) ? intakeElements.Max(e => e.Item2.CutInfo.CutSize) : 0;
            }

            if (commonIndex > -1)
            {
                currentX = referenceX + offsetLeft;

                for (int i = exhaustLeft.Count - 1; i >= 0; i--)
                {
                    currentX -= exhaustLeft[i].LengthTotal * MM_TO_FEET;
                    Element createdElement = CreateExhaustExtrusion(exhaustLeft[i], currentX, exhaustBaseZ);
                    if (createdElement != null)
                    {
                        exhaustElements.Add(new Tuple<Element, VentUnitItem>(createdElement, exhaustLeft[i]));
                    }
                }

                if(!_isIntakeBelow)
                {
                    double currentCommonX = referenceX;
                    foreach (var commonUnit in commonElements)
                    {
                        var matchingIntakeElement = intakeElements.FirstOrDefault(e => e.Item2.Id.Split('-').First() == commonUnit.Id.Split('-').First());
                        double length = matchingIntakeElement != null ? matchingIntakeElement.Item2.LengthTotal * MM_TO_FEET : commonUnit.LengthTotal * MM_TO_FEET;

                        double minX = currentCommonX;
                        double maxX = minX + length;
                        double minZ = exhaustBaseZ;

                        CreateFrameExtrusion(matchingIntakeElement.Item2, minX, maxX, minZ);

                        currentCommonX += length;
                    }
                }

                currentX = referenceX + totalCommonLength - offsetRight;
                foreach (var unit in exhaustRight)
                {
                    Element createdElement = CreateExhaustExtrusion(unit, currentX, exhaustBaseZ);
                    if (createdElement != null)
                    {
                        exhaustElements.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
                    }
                    currentX += unit.LengthTotal * MM_TO_FEET;
                }
            }
            else
            {
                foreach (var unit in _exhaustUnits)
                {
                    Element createdElement = CreateExhaustExtrusion(unit, currentX, exhaustBaseZ);
                    if (createdElement != null)
                    {
                        exhaustElements.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
                    }
                    currentX += unit.LengthTotal * MM_TO_FEET;
                }
            }
        }

        private Element CreateIntakeExtrusion(VentUnitItem unit, double startX, double baseZ)
        {
            return CreateExtrusion(unit, startX, baseZ, _maxWidth, false);
        }

        private Element CreateExhaustExtrusion(VentUnitItem unit, double startX, double baseZ)
        {
            return CreateExtrusion(unit, startX, baseZ, _maxWidth, true);
        }

        private Element CreateExtrusion(VentUnitItem unit, double startX, double baseZ, double maxWidth, bool isExhaust)
        {
            double length = unit.LengthTotal * MM_TO_FEET;
            double height = unit.HeightTotal * MM_TO_FEET;
            double width = unit.WidthTotal * MM_TO_FEET;

            bool isFlexibleDamper = unit.Children.Any(child => child.Type.Contains("flexibleDamper"));
            bool isAirValve = unit.Children.Any(child => child.Type.Contains("airValve"));
            bool isPlateUtilizer = unit.Children.Any(child => child.Type.Contains("plateUtilizer"));

            double minX = startX;
            double maxX = startX + length;
            double minZ = baseZ;

            bool isEndElement = isFlexibleDamper || isAirValve;

            if (isEndElement)
            {
                double heightOffset = (_isIntakeBelow ? _maxHeightIntake : _maxHeightExhaust) - height;

                if (_isIntakeBelow && _intakeUnits.Count > 0)
                {
                    minZ = isExhaust ? baseZ + heightOffset / 2 : -_maxHeightIntake / 2 + heightOffset / 2;
                }
                if (_isIntakeBelow && _intakeUnits.Count == 0)
                {
                    minZ = isExhaust ? baseZ + (_maxHeightExhaust - height) / 2 : -_maxHeightIntake / 2 + heightOffset / 2;
                }
                if (!_isIntakeBelow && _exhaustUnits.Count > 0)
                {
                    minZ = isExhaust ? baseZ + heightOffset / 2 : baseZ - heightOffset / 2;
                }
                if (!_isIntakeBelow && _exhaustUnits.Count == 0)
                {
                    heightOffset = _maxHeightIntake - height;
                    minZ = isExhaust ? baseZ + heightOffset / 2 : baseZ - heightOffset / 2;
                }
            }

            double maxZ = minZ + height;

            CurveArray curveArray = new CurveArray();
            if (isFlexibleDamper)
            {
                double inset = height / 6;

                XYZ p1 = new XYZ(minX, 0, minZ);
                XYZ p2 = new XYZ(minX + length / 2, 0, minZ + inset);
                XYZ p3 = new XYZ(maxX, 0, minZ);
                XYZ p4 = new XYZ(maxX, 0, maxZ);
                XYZ p5 = new XYZ(minX + length / 2, 0, maxZ - inset);
                XYZ p6 = new XYZ(minX, 0, maxZ);

                curveArray.Append(Line.CreateBound(p1, p2));
                curveArray.Append(Line.CreateBound(p2, p3));
                curveArray.Append(Line.CreateBound(p3, p4));
                curveArray.Append(Line.CreateBound(p4, p5));
                curveArray.Append(Line.CreateBound(p5, p6));
                curveArray.Append(Line.CreateBound(p6, p1));
            }
            if (isPlateUtilizer)
            {
                var (hasLeftCut, hasRightCut, cutSize) = GetPlateUtilizerCutInfo(unit);
                double topLength = length;
                double bottomLength = length;

                if (hasLeftCut) topLength -= cutSize;
                if (hasRightCut) topLength -= cutSize;

                XYZ p1 = new XYZ(minX, 0, minZ);
                XYZ p2 = new XYZ(maxX, 0, minZ);
                XYZ pRight_1 = new XYZ(maxX, 0, maxZ - height / 2);
                XYZ pRight_2 = new XYZ(maxX - cutSize, 0, maxZ - height / 2);
                XYZ pRight_3 = new XYZ(maxX - cutSize, 0, maxZ);
                XYZ p3 = new XYZ(maxX, 0, maxZ);
                XYZ pLeft_1 = new XYZ(minX + cutSize, 0, maxZ);
                XYZ pLeft_2 = new XYZ(minX + cutSize, 0, maxZ - height / 2);
                XYZ pLeft_3 = new XYZ(minX, 0, maxZ - height / 2);
                XYZ p4 = new XYZ(minX, 0, maxZ);

                if (hasLeftCut && hasRightCut)
                {
                    curveArray.Append(Line.CreateBound(p1, p2));
                    curveArray.Append(Line.CreateBound(p2, pRight_1));
                    curveArray.Append(Line.CreateBound(pRight_1, pRight_2));
                    curveArray.Append(Line.CreateBound(pRight_2, pRight_3));
                    curveArray.Append(Line.CreateBound(pRight_3, pLeft_1));
                    curveArray.Append(Line.CreateBound(pLeft_1, pLeft_2));
                    curveArray.Append(Line.CreateBound(pLeft_2, pLeft_3));
                    curveArray.Append(Line.CreateBound(pLeft_3, p1));
                }
                else if (hasLeftCut)
                {
                    curveArray.Append(Line.CreateBound(p1, p2));
                    curveArray.Append(Line.CreateBound(p2, p3));
                    curveArray.Append(Line.CreateBound(p3, pLeft_1));
                    curveArray.Append(Line.CreateBound(pLeft_1, pLeft_2));
                    curveArray.Append(Line.CreateBound(pLeft_2, pLeft_3));
                    curveArray.Append(Line.CreateBound(pLeft_3, p1));
                }
                else if (hasRightCut)
                {
                    curveArray.Append(Line.CreateBound(p1, p2));
                    curveArray.Append(Line.CreateBound(p2, p3));
                    curveArray.Append(Line.CreateBound(p3, pRight_1));
                    curveArray.Append(Line.CreateBound(pRight_1, pRight_2));
                    curveArray.Append(Line.CreateBound(pRight_2, pRight_3));
                    curveArray.Append(Line.CreateBound(pRight_3, p1));
                }
                else
                {
                    curveArray.Append(Line.CreateBound(p1, p2));
                    curveArray.Append(Line.CreateBound(p2, p3));
                    curveArray.Append(Line.CreateBound(p3, p4));
                    curveArray.Append(Line.CreateBound(p4, p1));
                }

                unit.CutInfo.HasLeftCut = hasLeftCut;
                unit.CutInfo.HasRightCut = hasRightCut;
                unit.CutInfo.CutSize = cutSize;
            }

            if (!isFlexibleDamper & !isPlateUtilizer)
            {
                curveArray.Append(Line.CreateBound(new XYZ(minX, 0, minZ), new XYZ(maxX, 0, minZ)));
                curveArray.Append(Line.CreateBound(new XYZ(maxX, 0, minZ), new XYZ(maxX, 0, maxZ)));
                curveArray.Append(Line.CreateBound(new XYZ(maxX, 0, maxZ), new XYZ(minX, 0, maxZ)));
                curveArray.Append(Line.CreateBound(new XYZ(minX, 0, maxZ), new XYZ(minX, 0, minZ)));
            }
            CurveArrArray curveArrArray = new CurveArrArray();
            curveArrArray.Append(curveArray);

            Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane sketchPlane = SketchPlane.Create(_doc, plane);

            Extrusion extrusion = _doc.FamilyCreate.NewExtrusion(true, curveArrArray, sketchPlane, width);

            double offset = (maxWidth - width) / 2;
            extrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(offset);
            extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(width + offset);

            if (unit.Category == "block" || unit.Category == "utilization_cross")
            {
                double accumulatedLength = 0;

                foreach (var child in unit.Children)
                {
                    if (child.Type.Contains("waterHeater") || child.Type.Contains("waterCooler") || child.Type.Contains("plateUtilizer"))
                    {
                        foreach (var pipe in child.Pipes)
                        {
                            CreatePipeExtrusion(startX + accumulatedLength, baseZ, pipe);
                        }
                    }

                    accumulatedLength += child.LengthTotal * MM_TO_FEET;
                }
            }

            double accumulatedLengthWindows = 0;
            foreach (var child in unit.Children)
            {
                if (child.Window != null)
                {
                    CreateWindowExtrusion(startX + accumulatedLengthWindows + (child.Window.X * MM_TO_FEET), baseZ, child.Window);
                }
                accumulatedLengthWindows += child.LengthTotal * MM_TO_FEET;
            }

            if ((!isEndElement && _isIntakeBelow && !isExhaust && _frameHeight >= 120) || (!isEndElement && !_isIntakeBelow && isExhaust && _frameHeight >= 120) ||
                (!isEndElement && !_isIntakeBelow && !isExhaust && _frameHeight >= 120 && _exhaustUnits.Count == 0) || (!isEndElement && _isIntakeBelow && isExhaust && _frameHeight >= 120 && _intakeUnits.Count == 0))
            {
                CreateFrameExtrusion(unit, minX, maxX, minZ);
            }

            return extrusion;
        }

        private void CreateFrameExtrusion(VentUnitItem unit, double minX, double maxX, double minZ)
        {
            double frameMinZ = Math.Round(minZ - _frameHeight * MM_TO_FEET, 2);
            double frameMaxZ = Math.Round(minZ, 2);
            double frameMidZ = Math.Round((frameMinZ + frameMaxZ) / 2, 2);
            double frameMidX = Math.Round((minX + maxX) / 2, 2);
            double roundedMinX = Math.Round(minX, 2);
            double roundedMaxX = Math.Round(maxX, 2);

            CurveArray frameCurves = new CurveArray();
            XYZ f1 = new XYZ(roundedMinX, 0, frameMinZ);
            XYZ f2 = new XYZ(roundedMaxX, 0, frameMinZ);
            XYZ f3 = new XYZ(roundedMaxX, 0, frameMaxZ);
            XYZ f4 = new XYZ(roundedMinX, 0, frameMaxZ);

            frameCurves.Append(Line.CreateBound(f1, f2));
            frameCurves.Append(Line.CreateBound(f2, f3));
            frameCurves.Append(Line.CreateBound(f3, f4));
            frameCurves.Append(Line.CreateBound(f4, f1));

            CurveArray holeMidCurves = new CurveArray();
            CurveArray holeLeftCurves = new CurveArray();
            CurveArray holeRightCurves = new CurveArray();

            if (unit.LengthTotal < 200)
            {
                XYZ rc1 = new XYZ(frameMidX - 0.25, 0, frameMidZ);
                XYZ rc2 = new XYZ(frameMidX - 0.15, 0, frameMidZ - 0.1);
                XYZ rc3 = new XYZ(frameMidX + 0.15, 0, frameMidZ - 0.1);
                XYZ rc4 = new XYZ(frameMidX + 0.25, 0, frameMidZ);
                XYZ rc5 = new XYZ(frameMidX + 0.15, 0, frameMidZ + 0.1);
                XYZ rc6 = new XYZ(frameMidX - 0.15, 0, frameMidZ + 0.1);

                holeMidCurves.Append(Line.CreateBound(rc1, rc2));
                holeMidCurves.Append(Line.CreateBound(rc2, rc3));
                holeMidCurves.Append(Line.CreateBound(rc3, rc4));
                holeMidCurves.Append(Line.CreateBound(rc4, rc5));
                holeMidCurves.Append(Line.CreateBound(rc5, rc6));
                holeMidCurves.Append(Line.CreateBound(rc6, rc1));
            }
            if (unit.LengthTotal >= 200 && unit.LengthTotal < 500)
            {
                XYZ rc1 = new XYZ(frameMidX - 0.35, 0, frameMidZ);
                XYZ rc2 = new XYZ(frameMidX - 0.25, 0, frameMidZ - 0.1);
                XYZ rc3 = new XYZ(frameMidX + 0.25, 0, frameMidZ - 0.1);
                XYZ rc4 = new XYZ(frameMidX + 0.35, 0, frameMidZ);
                XYZ rc5 = new XYZ(frameMidX + 0.25, 0, frameMidZ + 0.1);
                XYZ rc6 = new XYZ(frameMidX - 0.25, 0, frameMidZ + 0.1);

                holeMidCurves.Append(Line.CreateBound(rc1, rc2));
                holeMidCurves.Append(Line.CreateBound(rc2, rc3));
                holeMidCurves.Append(Line.CreateBound(rc3, rc4));
                holeMidCurves.Append(Line.CreateBound(rc4, rc5));
                holeMidCurves.Append(Line.CreateBound(rc5, rc6));
                holeMidCurves.Append(Line.CreateBound(rc6, rc1));
            }
            if (unit.LengthTotal >= 500)
            {
                XYZ rl1 = new XYZ(roundedMinX + 0.2, 0, frameMidZ);
                XYZ rl2 = new XYZ(roundedMinX + 0.3, 0, frameMidZ - 0.1);
                XYZ rl3 = new XYZ(roundedMinX + 0.8, 0, frameMidZ - 0.1);
                XYZ rl4 = new XYZ(roundedMinX + 0.9, 0, frameMidZ);
                XYZ rl5 = new XYZ(roundedMinX + 0.8, 0, frameMidZ + 0.1);
                XYZ rl6 = new XYZ(roundedMinX + 0.3, 0, frameMidZ + 0.1);

                holeLeftCurves.Append(Line.CreateBound(rl1, rl2));
                holeLeftCurves.Append(Line.CreateBound(rl2, rl3));
                holeLeftCurves.Append(Line.CreateBound(rl3, rl4));
                holeLeftCurves.Append(Line.CreateBound(rl4, rl5));
                holeLeftCurves.Append(Line.CreateBound(rl5, rl6));
                holeLeftCurves.Append(Line.CreateBound(rl6, rl1));

                XYZ rr1 = new XYZ(roundedMaxX - 0.2, 0, frameMidZ);
                XYZ rr2 = new XYZ(roundedMaxX - 0.3, 0, frameMidZ - 0.1);
                XYZ rr3 = new XYZ(roundedMaxX - 0.8, 0, frameMidZ - 0.1);
                XYZ rr4 = new XYZ(roundedMaxX - 0.9, 0, frameMidZ);
                XYZ rr5 = new XYZ(roundedMaxX - 0.8, 0, frameMidZ + 0.1);
                XYZ rr6 = new XYZ(roundedMaxX - 0.3, 0, frameMidZ + 0.1);

                holeRightCurves.Append(Line.CreateBound(rr1, rr2));
                holeRightCurves.Append(Line.CreateBound(rr2, rr3));
                holeRightCurves.Append(Line.CreateBound(rr3, rr4));
                holeRightCurves.Append(Line.CreateBound(rr4, rr5));
                holeRightCurves.Append(Line.CreateBound(rr5, rr6));
                holeRightCurves.Append(Line.CreateBound(rr6, rr1));
            }

            CurveArrArray frameCurveArrArray = new CurveArrArray();
            frameCurveArrArray.Append(frameCurves);

            if (holeMidCurves.Size > 0 || holeLeftCurves.Size > 0 || holeRightCurves.Size > 0)
            {
                frameCurveArrArray.Append(holeMidCurves);
                frameCurveArrArray.Append(holeLeftCurves);
                frameCurveArrArray.Append(holeRightCurves);
            }

            Plane framePlane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane frameSketchPlane = SketchPlane.Create(_doc, framePlane);

            _doc.FamilyCreate.NewExtrusion(true, frameCurveArrArray, frameSketchPlane, _maxWidth);
        }

        private void CreatePipeExtrusion(double startX, double baseZ, VentUnitPipe pipe)
        {
            double pipeX = startX + (pipe.X * MM_TO_FEET);
            double pipeZ = baseZ + (pipe.Y * MM_TO_FEET);
            double radius = (pipe.D / 2) * MM_TO_FEET;

            if (radius <= 0) return;

            XYZ center = new XYZ(pipeX, 0, pipeZ);

            CurveArray pipeCurve = new CurveArray();
            pipeCurve.Append(Arc.Create(center, radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisZ));

            CurveArrArray pipeCurves = new CurveArrArray();
            pipeCurves.Append(pipeCurve);

            Plane pipePlane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane pipeSketch = SketchPlane.Create(_doc, pipePlane);

            Extrusion pipeExtrusion = _doc.FamilyCreate.NewExtrusion(true, pipeCurves, pipeSketch, 2);

            pipeExtrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(0);
            pipeExtrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(-1);
        }

        private void CreateWindowExtrusion(double startX, double baseZ, VentUnitWindow window)
        {
            double windowX = startX;
            double windowZ = baseZ + (window.Y * MM_TO_FEET);
            double outerRadius = (window.D / 2) * MM_TO_FEET;
            double innerRadius = outerRadius - 20 * MM_TO_FEET;

            if (outerRadius <= 0 || innerRadius <= 0) return;

            XYZ center = new XYZ(windowX, 0, windowZ);

            CurveArray outerCurve = new CurveArray();
            outerCurve.Append(Arc.Create(center, outerRadius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisZ));

            CurveArray innerCurve = new CurveArray();
            innerCurve.Append(Arc.Create(center, innerRadius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisZ));

            CurveArrArray windowCurves = new CurveArrArray();
            windowCurves.Append(outerCurve);
            windowCurves.Append(innerCurve);

            Plane windowPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane windowSketch = SketchPlane.Create(_doc, windowPlane);

            Extrusion windowExtrusion = _doc.FamilyCreate.NewExtrusion(true, windowCurves, windowSketch, 2);

            windowExtrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(0);
            windowExtrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(-0.1);
        }

        private (bool hasLeftCut, bool hasRightCut, double cutSize) GetPlateUtilizerCutInfo(VentUnitItem unit)
        {
            var plateUtilizerChild = unit.Children.FirstOrDefault(child => child.Type.Contains("plateUtilizer"));
            if (plateUtilizerChild == null)
                return (false, false, 0);

            var allPanelSizes = plateUtilizerChild.ServicePanels.Select(p => p.SizesX).ToList();

            var topPanelSizes = allPanelSizes.Take(allPanelSizes.Count / 2).SelectMany(x => x).Distinct().ToList();
            var bottomPanelSizes = allPanelSizes.Skip(allPanelSizes.Count / 2).SelectMany(x => x).Distinct().ToList();

            var uniqueTop = topPanelSizes.Except(bottomPanelSizes).ToList();
            var uniqueBottom = bottomPanelSizes.Except(topPanelSizes).ToList();

            bool hasLeftCut = uniqueBottom.Any() && bottomPanelSizes.First() == uniqueBottom.First();
            bool hasRightCut = uniqueBottom.Any() && bottomPanelSizes.Last() == uniqueBottom.First();
            double cutSize = uniqueBottom.FirstOrDefault() * MM_TO_FEET;

            return (hasLeftCut, hasRightCut, cutSize);
        }

    }
}
