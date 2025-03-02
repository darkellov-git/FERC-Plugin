using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace FERCPlugin.Core.Models
{
    public class DuctConnectorCreator
    {
        private readonly Document _doc;
        private readonly List<Tuple<Element, VentUnitItem>> _flexibleDampers;

        private const double MM_TO_FEET = 0.00328084;

        public DuctConnectorCreator(Document doc, List<Tuple<Element, VentUnitItem>> flexibleDampers)
        {
            _doc = doc;
            _flexibleDampers = flexibleDampers;
        }

        public void CreateConnectors()
        {
            using (Transaction tx = new Transaction(_doc, "Create Duct Connectors"))
            {
                tx.Start();

                foreach (var (element, unit) in _flexibleDampers)
                {
                    CreateConnectorForFlexibleDamper(element, unit);
                }

                tx.Commit();
            }
        }

        private void CreateConnectorForFlexibleDamper(Element element, VentUnitItem unit)
        {
            Options options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = true };
            GeometryElement geomElement = element.get_Geometry(options);

            if (geomElement == null) return;

            List<PlanarFace> candidateFaces = new List<PlanarFace>();

            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace && IsVerticalFace(planarFace))
                        {
                            candidateFaces.Add(planarFace);
                        }
                    }
                }
            }

            if (candidateFaces.Count == 0) return;

            PlanarFace selectedFace = GetExtremeLargestFace(candidateFaces);

            if (selectedFace == null) return;

            ConnectorElement connector = ConnectorElement.CreateDuctConnector(
                _doc,
                DuctSystemType.Global,
                ConnectorProfileType.Rectangular,
                selectedFace.Reference
            );

            if (connector == null) return;

            Parameter widthParam = connector.LookupParameter("Width");
            if (widthParam != null) widthParam.Set(unit.WidthTotal * MM_TO_FEET);

            Parameter heightParam = connector.LookupParameter("Height");
            if (heightParam != null) heightParam.Set(unit.HeightTotal * MM_TO_FEET);
        }

        private bool IsVerticalFace(PlanarFace face)
        {
            XYZ normal = face.FaceNormal;

            return Math.Abs(normal.X) > 0.99 && Math.Abs(normal.Y) < 0.01 && Math.Abs(normal.Z) < 0.01;
        }

        private PlanarFace GetExtremeLargestFace(List<PlanarFace> faces)
        {
            PlanarFace extremeFace = null;
            double extremeX = double.MinValue;
            double extremeNegativeX = double.MaxValue;
            double maxArea = 0;

            foreach (var face in faces)
            {
                if (!IsVerticalFace(face)) continue;

                double faceX = face.Origin.X;
                double area = GetFaceArea(face);

                if (faceX >= 0)
                {
                    if (faceX > extremeX || (faceX == extremeX && area > maxArea))
                    {
                        extremeX = faceX;
                        maxArea = area;
                        extremeFace = face;
                    }
                }
                else
                {
                    if (faceX < extremeNegativeX || (faceX == extremeNegativeX && area > maxArea))
                    {
                        extremeNegativeX = faceX;
                        maxArea = area;
                        extremeFace = face;
                    }
                }
            }

            return extremeFace;
        }

        private double GetFaceArea(PlanarFace face)
        {
            BoundingBoxUV bbox = face.GetBoundingBox();
            double width = bbox.Max.U - bbox.Min.U;
            double height = bbox.Max.V - bbox.Min.V;

            return width * height; 
        }
    }
}
