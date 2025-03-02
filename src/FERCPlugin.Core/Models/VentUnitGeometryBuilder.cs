using Autodesk.Revit.DB;

namespace FERCPlugin.Core.Models
{
    public class VentUnitGeometryBuilder
    {
        private readonly Document _doc;
        private readonly List<VentUnitItem> _intakeUnits;
        private readonly List<VentUnitItem> _exhaustUnits;

        private double _maxHeightIntake;
        private double _maxHeightExhaust;
        private double _maxWidth;
        private double _totalLengthIntake;
        private double _totalLengthExhaust;

        private const double MM_TO_FEET = 0.00328084;

        public VentUnitGeometryBuilder(Document doc, List<VentUnitItem> intakeUnits, List<VentUnitItem> exhaustUnits)
        {
            _doc = doc;
            _intakeUnits = intakeUnits;
            _exhaustUnits = exhaustUnits;

            _maxHeightIntake = _intakeUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
                .Max(unit => unit.HeightTotal) * MM_TO_FEET;

            _maxHeightExhaust = _exhaustUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
                .Max(unit => unit.HeightTotal) * MM_TO_FEET;

            _maxWidth = _intakeUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("Utilizer"))
                .Max(unit => unit.WidthTotal) * MM_TO_FEET;

            _totalLengthIntake = _intakeUnits.Sum(unit => unit.LengthTotal) * MM_TO_FEET;
            _totalLengthExhaust = _exhaustUnits.Sum(unit => unit.LengthTotal) * MM_TO_FEET;
        }

        public void BuildGeometry()
        {
            using (Transaction tx = new Transaction(_doc, "Build Vent Unit Geometry"))
            {
                tx.Start();

                Dictionary<string, double> intakePositions = CreateIntakeGeometry();

                CreateExhaustGeometry(intakePositions);

                tx.Commit();
            }
        }

        private Dictionary<string, double> CreateIntakeGeometry()
        {
            double currentX = -_totalLengthIntake / 2;
            Dictionary<string, double> intakePositions = new Dictionary<string, double>();

            foreach (var unit in _intakeUnits)
            {
                CreateIntakeExtrusion(unit, currentX);

                foreach (var child in unit.Children)
                {
                    if (!intakePositions.ContainsKey(child.Id))
                        intakePositions[child.Id] = currentX;
                }

                currentX += unit.LengthTotal * MM_TO_FEET;
            }

            return intakePositions;
        }

        private void CreateExhaustGeometry(Dictionary<string, double> intakePositions)
        {
            double exhaustZCenter = _maxHeightIntake / 2 + _maxHeightExhaust / 2;
            List<VentUnitItem> commonElements = new List<VentUnitItem>();
            List<VentUnitItem> exhaustLeft = new List<VentUnitItem>();
            List<VentUnitItem> exhaustRight = new List<VentUnitItem>();

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
                CreateExhaustExtrusion(exhaustLeft[i], currentX, exhaustZCenter);
            }

            currentX = referenceX + totalCommonLength;
            foreach (var unit in exhaustRight)
            {
                CreateExhaustExtrusion(unit, currentX, exhaustZCenter);
                currentX += unit.LengthTotal * MM_TO_FEET;
            }
        }


        private void CreateExhaustExtrusion(VentUnitItem unit, double startX, double centerZ)
        {
            double length = unit.LengthTotal * MM_TO_FEET;
            double height = unit.HeightTotal * MM_TO_FEET;
            double width = unit.WidthTotal * MM_TO_FEET;

            bool isFlexibleDamper = unit.Children.Any(child => child.Type.Contains("flexibleDamper"));
            bool isAirValve = unit.Children.Any(child => child.Type.Contains("airValve"));

            double minX = startX;
            double maxX = startX + length;

            double minZ = centerZ - (height / 2);
            double maxZ = minZ + height;

            CurveLoop curveLoop = new CurveLoop();

            if (isFlexibleDamper)
            {
                double inset = height / 5;

                XYZ p1 = new XYZ(minX, 0, minZ);
                XYZ p2 = new XYZ(minX + length / 2, 0, minZ + inset);
                XYZ p3 = new XYZ(maxX, 0, minZ);
                XYZ p4 = new XYZ(maxX, 0, maxZ);
                XYZ p5 = new XYZ(minX + length / 2, 0, maxZ - inset);
                XYZ p6 = new XYZ(minX, 0, maxZ);

                curveLoop.Append(Line.CreateBound(p1, p2));
                curveLoop.Append(Line.CreateBound(p2, p3));
                curveLoop.Append(Line.CreateBound(p3, p4));
                curveLoop.Append(Line.CreateBound(p4, p5));
                curveLoop.Append(Line.CreateBound(p5, p6));
                curveLoop.Append(Line.CreateBound(p6, p1));
            }
            else
            {
                curveLoop.Append(Line.CreateBound(new XYZ(minX, 0, minZ), new XYZ(maxX, 0, minZ)));
                curveLoop.Append(Line.CreateBound(new XYZ(maxX, 0, minZ), new XYZ(maxX, 0, maxZ)));
                curveLoop.Append(Line.CreateBound(new XYZ(maxX, 0, maxZ), new XYZ(minX, 0, maxZ)));
                curveLoop.Append(Line.CreateBound(new XYZ(minX, 0, maxZ), new XYZ(minX, 0, minZ)));
            }

            Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane sketchPlane = SketchPlane.Create(_doc, plane);

            CurveArrArray curveArrArray = new CurveArrArray();
            CurveArray curveArray = new CurveArray();
            foreach (Curve curve in curveLoop)
            {
                curveArray.Append(curve);
            }
            curveArrArray.Append(curveArray);

            Extrusion extrusion = _doc.FamilyCreate.NewExtrusion(true, curveArrArray, sketchPlane, width);

            if (isFlexibleDamper || isAirValve)
            {
                double offset = (_maxWidth - width) / 2;
                extrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(offset);
                extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(width + offset);
            }

            Parameter lengthParam = extrusion.LookupParameter("Length");
            if (lengthParam != null) lengthParam.Set(length);
            Parameter heightParam = extrusion.LookupParameter("Height");
            if (heightParam != null) heightParam.Set(height);
            Parameter widthParam = extrusion.LookupParameter("Width");
            if (widthParam != null) widthParam.Set(width);
        }


        private void CreateIntakeExtrusion(VentUnitItem unit, double startX)
        {
            double length = unit.LengthTotal * MM_TO_FEET;
            double height = unit.HeightTotal * MM_TO_FEET;
            double width = unit.WidthTotal * MM_TO_FEET;

            bool isFlexibleDamper = unit.Children.Any(child => child.Type.Contains("flexibleDamper"));
            bool isAirValve = unit.Children.Any(child => child.Type.Contains("airValve"));

            double minX = startX;
            double maxX = startX + length;

            double minZ;
            if (isFlexibleDamper || isAirValve)
            {
                minZ = -_maxHeightIntake / 2 + ((_maxHeightIntake - height) / 2);
            }
            else
            {
                minZ = -_maxHeightIntake / 2;
            }
            double maxZ = minZ + height;

            CurveLoop curveLoop = new CurveLoop();

            if (isFlexibleDamper)
            {
                double inset = height / 5;

                XYZ p1 = new XYZ(minX, 0, minZ);
                XYZ p2 = new XYZ(minX + length / 2, 0, minZ + inset);
                XYZ p3 = new XYZ(maxX, 0, minZ);
                XYZ p4 = new XYZ(maxX, 0, maxZ);
                XYZ p5 = new XYZ(minX + length / 2, 0, maxZ - inset);
                XYZ p6 = new XYZ(minX, 0, maxZ);

                curveLoop.Append(Line.CreateBound(p1, p2));
                curveLoop.Append(Line.CreateBound(p2, p3));
                curveLoop.Append(Line.CreateBound(p3, p4));
                curveLoop.Append(Line.CreateBound(p4, p5));
                curveLoop.Append(Line.CreateBound(p5, p6));
                curveLoop.Append(Line.CreateBound(p6, p1));
            }
            else
            {
                curveLoop.Append(Line.CreateBound(new XYZ(minX, 0, minZ), new XYZ(maxX, 0, minZ)));
                curveLoop.Append(Line.CreateBound(new XYZ(maxX, 0, minZ), new XYZ(maxX, 0, maxZ)));
                curveLoop.Append(Line.CreateBound(new XYZ(maxX, 0, maxZ), new XYZ(minX, 0, maxZ)));
                curveLoop.Append(Line.CreateBound(new XYZ(minX, 0, maxZ), new XYZ(minX, 0, minZ)));
            }

            Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero);
            SketchPlane sketchPlane = SketchPlane.Create(_doc, plane);

            CurveArrArray curveArrArray = new CurveArrArray();
            CurveArray curveArray = new CurveArray();
            foreach (Curve curve in curveLoop)
            {
                curveArray.Append(curve);
            }
            curveArrArray.Append(curveArray);

            Extrusion extrusion = _doc.FamilyCreate.NewExtrusion(true, curveArrArray, sketchPlane, width);

            if (isFlexibleDamper || isAirValve)
            {
                double offset = (_maxWidth - width) / 2;
                extrusion.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(offset);
                extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(width + offset);
            }

            Parameter lengthParam = extrusion.LookupParameter("Length");
            if (lengthParam != null) lengthParam.Set(length);

            Parameter heightParam = extrusion.LookupParameter("Height");
            if (heightParam != null) heightParam.Set(height);

            Parameter widthParam = extrusion.LookupParameter("Width");
            if (widthParam != null) widthParam.Set(width);
        }
    }
}
