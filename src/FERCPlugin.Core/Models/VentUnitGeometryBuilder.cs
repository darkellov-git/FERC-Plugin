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

        public List<Tuple<Element, VentUnitItem>> BuildGeometry()
        {
            List<Tuple<Element, VentUnitItem>> flexibleDampers = new();

            using (Transaction tx = new Transaction(_doc, "Build Vent Unit Geometry"))
            {
                tx.Start();

                Dictionary<string, double> intakePositions = _isIntakeBelow
                    ? CreateIntakeGeometry(flexibleDampers, -_maxHeightIntake / 2)
                    : CreateIntakeGeometry(flexibleDampers, _maxHeightExhaust);

                CreateExhaustGeometry(intakePositions, flexibleDampers);

                tx.Commit();
            }

            return flexibleDampers;
        }

        private Dictionary<string, double> CreateIntakeGeometry(List<Tuple<Element, VentUnitItem>> flexibleDampers, double intakeBaseZ)
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

                if (createdElement != null && unit.Children.Any(child => child.Type.Contains("flexibleDamper")))
                {
                    flexibleDampers.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
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

        private void CreateExhaustGeometry(Dictionary<string, double> intakePositions, List<Tuple<Element, VentUnitItem>> flexibleDampers)
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

                        totalCommonLength += unit.LengthTotal * MM_TO_FEET;
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

            double currentX = referenceX;
            for (int i = exhaustLeft.Count - 1; i >= 0; i--)
            {
                currentX -= exhaustLeft[i].LengthTotal * MM_TO_FEET;
                Element createdElement = CreateExhaustExtrusion(exhaustLeft[i], currentX, exhaustBaseZ);
                if (createdElement != null && exhaustLeft[i].Children.Any(child => child.Type.Contains("flexibleDamper")))
                {
                    flexibleDampers.Add(new Tuple<Element, VentUnitItem>(createdElement, exhaustLeft[i]));
                }
            }

            currentX = referenceX + totalCommonLength;
            foreach (var unit in exhaustRight)
            {
                Element createdElement = CreateExhaustExtrusion(unit, currentX, exhaustBaseZ);
                if (createdElement != null && unit.Children.Any(child => child.Type.Contains("flexibleDamper")))
                {
                    flexibleDampers.Add(new Tuple<Element, VentUnitItem>(createdElement, unit));
                }
                currentX += unit.LengthTotal * MM_TO_FEET;
            }
        }

        private Element CreateExhaustExtrusion(VentUnitItem unit, double startX, double baseZ)
        {
            return CreateExtrusion(unit, startX, baseZ, _maxWidth, true);
        }

        private Element CreateIntakeExtrusion(VentUnitItem unit, double startX, double baseZ)
        {
            return CreateExtrusion(unit, startX, baseZ, _maxWidth, false);
        }

        private Element CreateExtrusion(VentUnitItem unit, double startX, double baseZ, double maxWidth, bool isExhaust)
        {
            double length = unit.LengthTotal * MM_TO_FEET;
            double height = unit.HeightTotal * MM_TO_FEET;
            double width = unit.WidthTotal * MM_TO_FEET;

            bool isFlexibleDamper = unit.Children.Any(child => child.Type.Contains("flexibleDamper"));
            bool isAirValve = unit.Children.Any(child => child.Type.Contains("airValve"));

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
            curveArray.Append(Line.CreateBound(new XYZ(minX, 0, minZ), new XYZ(maxX, 0, minZ)));
            curveArray.Append(Line.CreateBound(new XYZ(maxX, 0, minZ), new XYZ(maxX, 0, maxZ)));
            curveArray.Append(Line.CreateBound(new XYZ(maxX, 0, maxZ), new XYZ(minX, 0, maxZ)));
            curveArray.Append(Line.CreateBound(new XYZ(minX, 0, maxZ), new XYZ(minX, 0, minZ)));

            CurveArrArray curveArrArray = new CurveArrArray();
            curveArrArray.Append(curveArray);

            Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane sketchPlane = SketchPlane.Create(_doc, plane);

            Extrusion extrusion = _doc.FamilyCreate.NewExtrusion(true, curveArrArray, sketchPlane, width);

            double offset = (maxWidth - width) / 2;
            extrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(offset);
            extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(width + offset);

            return extrusion;
        }
    }
}
