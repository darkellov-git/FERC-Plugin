using Autodesk.Revit.DB;

namespace FERCPlugin.Core.Models
{
    public class VentUnitGeometryBuilder
    {
        private readonly Document _doc;
        private readonly List<VentUnitItem> _intakeUnits;
        private readonly List<VentUnitItem> _exhaustUnits;
        private readonly bool _isIntakeBelow;

        private double _maxHeightIntake;
        private double _maxHeightExhaust;
        private double _maxWidth;
        private double _totalLengthIntake;
        private double _totalLengthExhaust;

        private const double MM_TO_FEET = 0.00328084;

        public VentUnitGeometryBuilder(Document doc, List<VentUnitItem> intakeUnits, List<VentUnitItem> exhaustUnits, bool isIntakeBelow)
        {
            _doc = doc;
            _intakeUnits = intakeUnits;
            _exhaustUnits = exhaustUnits;
            _isIntakeBelow = isIntakeBelow;

            _maxHeightIntake = GetMaxHeight(_intakeUnits);
            _maxHeightExhaust = GetMaxHeight(_exhaustUnits);
            _maxWidth = GetMaxWidth(_intakeUnits);
            _totalLengthIntake = GetTotalLength(_intakeUnits);
            _totalLengthExhaust = GetTotalLength(_exhaustUnits);
        }

        private static double GetMaxHeight(List<VentUnitItem> units) =>
    units.Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
         .Max(unit => unit.HeightTotal) * MM_TO_FEET;

        private static double GetMaxWidth(List<VentUnitItem> units) =>
            units.Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
                 .Max(unit => unit.WidthTotal) * MM_TO_FEET;

        private static double GetTotalLength(List<VentUnitItem> units) =>
            units.Sum(unit => unit.LengthTotal) * MM_TO_FEET;

        public (List<Tuple<Element, VentUnitItem>>, List<Tuple<Element, VentUnitItem>>, double, double) BuildGeometry()
        {
            List<Tuple<Element, VentUnitItem>> intakeElements = new();
            List<Tuple<Element, VentUnitItem>> exhaustElements = new();

            using (Transaction tx = new Transaction(_doc, "Build Vent Unit Geometry"))
            {
                tx.Start();

                Dictionary<string, double> intakePositions = _isIntakeBelow
                    ? CreateIntakeGeometry(intakeElements, -_maxHeightIntake / 2)
                    : CreateIntakeGeometry(intakeElements, _maxHeightExhaust);

                CreateExhaustGeometry(intakePositions, exhaustElements, intakeElements);

                tx.Commit();
            }

            return (intakeElements, exhaustElements, _maxHeightIntake, _maxHeightExhaust);
        }

        private Dictionary<string, double> CreateIntakeGeometry(List<Tuple<Element, VentUnitItem>> intakeElements, double intakeBaseZ)
        {
            double currentX = -_totalLengthIntake / 2;
            Dictionary<string, double> intakePositions = new();

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

            return intakePositions;
        }

        private void CreateExhaustGeometry(Dictionary<string, double> intakePositions, List<Tuple<Element, VentUnitItem>> exhaustElements, List<Tuple<Element, VentUnitItem>> intakeElements)
        {
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
                        break;
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
            double offsetLeft = intakeElements.Any(e => e.Item2.CutInfo.HasLeftCut) ? intakeElements.Max(e => e.Item2.CutInfo.CutSize) : 0;
            double offsetRight = intakeElements.Any(e => e.Item2.CutInfo.HasRightCut) ? intakeElements.Max(e => e.Item2.CutInfo.CutSize) : 0;

            double currentX = referenceX + offsetLeft;
            for (int i = exhaustLeft.Count - 1; i >= 0; i--)
            {
                currentX -= exhaustLeft[i].LengthTotal * MM_TO_FEET;
                Element createdElement = CreateExhaustExtrusion(exhaustLeft[i], currentX, exhaustBaseZ);
                if (createdElement != null)
                {
                    exhaustElements.Add(new Tuple<Element, VentUnitItem>(createdElement, exhaustLeft[i]));
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

                if (_isIntakeBelow)
                {
                    minZ = isExhaust ? baseZ + heightOffset / 2 : -_maxHeightIntake / 2 + heightOffset / 2;
                }
                else
                {
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
                var plateUtilizerChild = unit.Children.FirstOrDefault(child => child.Type.Contains("plateUtilizer"));
                var topPanelSizes = plateUtilizerChild.ServicePanels[0].SizesX;
                var bottomPanelSizes = plateUtilizerChild.ServicePanels[1].SizesX;

                var uniqueTop = topPanelSizes.Except(bottomPanelSizes).ToList();
                var uniqueBottom = bottomPanelSizes.Except(topPanelSizes).ToList();

                bool hasLeftCut = uniqueBottom.Count > 0 && bottomPanelSizes.First() == uniqueBottom[0];
                bool hasRightCut = uniqueBottom.Count > 0 && bottomPanelSizes.Last() == uniqueBottom[0];

                double cutSize = uniqueBottom.FirstOrDefault() * MM_TO_FEET;
                double topLength = length;
                double bottomLength = length;

                if (hasLeftCut) topLength -= cutSize;
                if (hasRightCut) topLength -= cutSize;

                XYZ p1 = new XYZ(minX, 0, minZ);
                XYZ p2 = new XYZ(maxX, 0, minZ);
                XYZ pRight_1 = new XYZ(maxX, 0, maxZ - height/2);
                XYZ pRight_2 = new XYZ(maxX - cutSize, 0, maxZ - height / 2);
                XYZ pRight_3 = new XYZ(maxX - cutSize, 0, maxZ);
                XYZ p3 = new XYZ(maxX, 0, maxZ);
                XYZ pLeft_1 = new XYZ(minX + cutSize, 0, maxZ);
                XYZ pLeft_2 = new XYZ(minX + cutSize, 0, maxZ - height / 2);
                XYZ pLeft_3 = new XYZ(minX, 0, maxZ - height / 2);
                XYZ p4 = new XYZ(minX, 0, maxZ);

                if (hasLeftCut)
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
                    curveArray.Append(Line.CreateBound(p2, pRight_1));
                    curveArray.Append(Line.CreateBound(pRight_1, pRight_2));
                    curveArray.Append(Line.CreateBound(pRight_2, pRight_3));
                    curveArray.Append(Line.CreateBound(pRight_3, pLeft_1));
                    curveArray.Append(Line.CreateBound(pLeft_1, pLeft_2));
                    curveArray.Append(Line.CreateBound(pLeft_2, pLeft_3));
                    curveArray.Append(Line.CreateBound(pLeft_3, p1));
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

            return extrusion;
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
    }
}
