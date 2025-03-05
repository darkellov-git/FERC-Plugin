using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FERCPlugin.Core.Models
{
    public class AnnotationBuilder
    {
        private readonly Document _doc;
        private readonly List<Tuple<Element, VentUnitItem>> _intakeElements;
        private readonly List<Tuple<Element, VentUnitItem>> _exhaustElements;
        private readonly bool _hasUtilizationCross;
        private readonly bool _isIntakeBelow;
        private readonly View _frontView;

        private const double DIM_OFFSET_LARGE = 4.0; 
        private const double DIM_OFFSET_SMALL = 3.0; 
        private const double MM_TO_FEET = 0.00328084;

        public AnnotationBuilder(
            Document doc,
            List<Tuple<Element, VentUnitItem>> intakeElements,
            List<Tuple<Element, VentUnitItem>> exhaustElements,
            bool hasUtilizationCross,
            bool isIntakeBelow)
        {
            _doc = doc;
            _intakeElements = intakeElements;
            _exhaustElements = exhaustElements;
            _hasUtilizationCross = hasUtilizationCross;
            _isIntakeBelow = isIntakeBelow;
            _frontView = GetFrontView();
        }

        public void AddAnnotations()
        {
            if (_frontView == null)
            {
                TaskDialog.Show("Ошибка", "Вид Front не найден.");
                return;
            }

            using (Transaction tx = new Transaction(_doc, "Add Annotations"))
            {
                tx.Start();
                CreateHorizontalDimensions(_intakeElements, _isIntakeBelow ? -DIM_OFFSET_SMALL : DIM_OFFSET_LARGE);
                CreateHorizontalDimensions(_exhaustElements, _isIntakeBelow ? DIM_OFFSET_LARGE : -DIM_OFFSET_SMALL);
                CreateVerticalDimensions(_intakeElements, DIM_OFFSET_LARGE);
                CreateVerticalDimensions(_exhaustElements, DIM_OFFSET_LARGE);
                tx.Commit();
            }
        }

        private View GetFrontView()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.ViewDirection.IsAlmostEqualTo(XYZ.BasisY));
        }

        private void CreateHorizontalDimensions(List<Tuple<Element, VentUnitItem>> elements, double offset)
        {
            if (!elements.Any()) return;

            foreach (var (element, _) in elements)
            {
                List<Reference> verticalFaces = GetParallelFaces(element, XYZ.BasisX);
                if (verticalFaces.Count < 2) continue;

                Reference leftFace = verticalFaces.OrderBy(f => GetFaceCenter(f).X).First();
                Reference rightFace = verticalFaces.OrderBy(f => GetFaceCenter(f).X).Last();

                XYZ dimLinePosition = GetFaceCenter(leftFace) + new XYZ(0, 0, offset);
                CreateDimension(leftFace, rightFace, dimLinePosition);
            }
        }

        private void CreateVerticalDimensions(List<Tuple<Element, VentUnitItem>> elements, double offset)
        {
            if (!elements.Any()) return;

            foreach (var (element, _) in elements)
            {
                List<Reference> horizontalFaces = GetParallelFaces(element, XYZ.BasisZ);
                if (horizontalFaces.Count < 2) continue;

                Reference topFace = horizontalFaces.OrderBy(f => GetFaceCenter(f).Z).Last();
                Reference bottomFace = horizontalFaces.OrderBy(f => GetFaceCenter(f).Z).First();

                XYZ dimLinePosition = GetFaceCenter(bottomFace) + new XYZ(offset, 0, 0);
                CreateDimension(bottomFace, topFace, dimLinePosition);
            }
        }

        private List<Reference> GetParallelFaces(Element element, XYZ normal)
        {
            List<Reference> faces = new();
            Options options = new() { ComputeReferences = true };
            GeometryElement geomElem = element.get_Geometry(options);

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        PlanarFace planarFace = face as PlanarFace;
                        if (planarFace != null && planarFace.FaceNormal.IsAlmostEqualTo(normal))
                        {
                            faces.Add(planarFace.Reference);
                        }
                    }
                }
            }

            return faces;
        }

        private XYZ GetFaceCenter(Reference faceRef)
        {
            Face face = _doc.GetElement(faceRef.ElementId).GetGeometryObjectFromReference(faceRef) as Face;
            if (face == null)
                return XYZ.Zero;

            BoundingBoxUV bbox = face.GetBoundingBox();
            UV centerUV = (bbox.Min + bbox.Max) / 2;
            return face.Evaluate(centerUV);
        }


        private void CreateDimension(Reference ref1, Reference ref2, XYZ dimLinePosition)
        {
            Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX);

            ReferenceArray refArray = new ReferenceArray();
            refArray.Append(ref1);
            refArray.Append(ref2);

            _doc.Create.NewDimension(_frontView, dimLine, refArray);
        }

    }
}
