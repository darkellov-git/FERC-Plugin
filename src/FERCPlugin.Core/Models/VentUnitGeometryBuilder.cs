using Autodesk.Revit.DB;

namespace FERCPlugin.Core.Models
{
    public class VentUnitGeometryBuilder
    {
        private readonly Document _doc;
        private readonly List<VentUnitItem> _intakeUnits;
        private double _maxHeight;
        private double _maxWidth;
        private double _totalLength;

        private const double MM_TO_FEET = 0.00328084;

        public VentUnitGeometryBuilder(Document doc, List<VentUnitItem> intakeUnits)
        {
            _doc = doc;
            _intakeUnits = intakeUnits;

            _maxHeight = _intakeUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("utilization"))
                .Max(unit => unit.HeightTotal) * MM_TO_FEET;

            _maxWidth = _intakeUnits
                .Where(unit => !unit.Category.Contains("recirculator") && !unit.Category.Contains("Utilizer"))
                .Max(unit => unit.WidthTotal) * MM_TO_FEET;

            _totalLength = _intakeUnits.Sum(unit => unit.LengthTotal) * MM_TO_FEET;
        }

        public void BuildGeometry()
        {
            using (Transaction tx = new Transaction(_doc, "Build Vent Unit Geometry"))
            {
                tx.Start();

                double currentX = -_totalLength / 2; 

                foreach (var unit in _intakeUnits)
                {
                    CreateExtrusion(unit, currentX);
                    currentX += unit.LengthTotal * MM_TO_FEET;
                }

                tx.Commit();
            }
        }

        private void CreateExtrusion(VentUnitItem unit, double startX)
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
                minZ = -_maxHeight / 2 + ((_maxHeight - height) / 2);
            }
            else
            {
                minZ = -_maxHeight / 2;
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

            Parameter lengthParam = extrusion.LookupParameter("Length");
            if (lengthParam != null) lengthParam.Set(length);

            Parameter heightParam = extrusion.LookupParameter("Height");
            if (heightParam != null) heightParam.Set(height);

            Parameter widthParam = extrusion.LookupParameter("Width");
            if (widthParam != null) widthParam.Set(width);
        }
    }
}
