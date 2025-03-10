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
        private static double _halfHeightIntake;

        private const double MM_TO_FEET = 0.00328084;

        public AnnotationBuilder(
            Document doc,
            List<Tuple<Element, VentUnitItem>> intakeElements,
            List<Tuple<Element, VentUnitItem>> exhaustElements,
            bool hasUtilizationCross,
            bool isIntakeBelow,
            double maxHeightIntake,
            double maxHeightExhaust)
        {
            _doc = doc;
            _intakeElements = intakeElements;
            _exhaustElements = exhaustElements;
            _hasUtilizationCross = hasUtilizationCross;
            _isIntakeBelow = isIntakeBelow;
            _halfHeightIntake = maxHeightIntake / 2;
            _frontView = GetFrontView();
        }

        public void AddAnnotations()
        {
            if (_frontView == null)
            {
                TaskDialog.Show("Ошибка", "Вид Front не найден.");
                return;
            }

            CreateHorizontalDimensions(_intakeElements, _isIntakeBelow ? -_halfHeightIntake - 1 : _halfHeightIntake + 1, false);
            CreateHorizontalDimensions(_exhaustElements, _isIntakeBelow ? _halfHeightIntake + 1 : -_halfHeightIntake - 1, true);
            CreateVerticalDimensions(_intakeElements, _exhaustElements);
        }

        private View GetFrontView()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.Name.Equals("Front", StringComparison.OrdinalIgnoreCase)
                                     && v.ViewType == ViewType.Elevation);
        }

        private void CreateHorizontalDimensions(List<Tuple<Element, VentUnitItem>> elements, double offset, bool isExhaust)
        {
            using (Transaction tx = new(_doc, "Creating horizontal dimensions"))
            {
                tx.Start();

                if (elements.Count < 2) return;

                List<Tuple<Reference, Reference, double>> facePairs = new();

                foreach (var (element, _) in elements)
                {
                    List<Reference> verticalFaces = GetParallelFaces(element, XYZ.BasisX);
                    if (verticalFaces.Count < 2) continue;

                    Reference leftFace = verticalFaces.OrderBy(f => GetFaceCenter(f).X).First();
                    Reference rightFace = verticalFaces.OrderBy(f => GetFaceCenter(f).X).Last();

                    double leftX = GetFaceCenter(leftFace).X;
                    double rightX = GetFaceCenter(rightFace).X;

                    facePairs.Add(Tuple.Create(leftFace, rightFace, leftX));
                }

                facePairs = facePairs.OrderBy(pair => pair.Item3).ToList();

                List<Reference> leftFaces = facePairs.Select(pair => pair.Item1).ToList();
                List<Reference> rightFaces = facePairs.Select(pair => pair.Item2).ToList();

                if (!isExhaust)
                {
                    ReferenceArray refArray = new ReferenceArray();
                    for (int i = 0; i < leftFaces.Count - 1; i++)
                    {
                        refArray.Append(leftFaces[i]);
                    }

                    refArray.Append(leftFaces.Last());
                    refArray.Append(rightFaces.Last());

                    if (refArray.Size > 1)
                    {
                        XYZ dimLinePosition = GetFaceCenter(leftFaces.First()) + new XYZ(0, 0, offset);
                        Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);
                        _doc.FamilyCreate.NewDimension(_frontView, dimLine, refArray);
                    }
                }
                else
                {
                    for (int i = 0; i < leftFaces.Count; i++)
                    {
                        ReferenceArray singleRefArray = new ReferenceArray();
                        singleRefArray.Append(leftFaces[i]);
                        singleRefArray.Append(rightFaces[i]);

                        XYZ dimLinePosition = GetFaceCenter(leftFaces[i]) + new XYZ(0, 0, offset);
                        Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);

                        _doc.FamilyCreate.NewDimension(_frontView, dimLine, singleRefArray);
                    }

                    for (int i = 0; i < rightFaces.Count - 1; i++)
                    {
                        XYZ rightFacePos = GetFaceCenter(rightFaces[i]);
                        XYZ leftFaceNextPos = GetFaceCenter(leftFaces[i + 1]);

                        double gap = leftFaceNextPos.X - rightFacePos.X;

                        if (gap > 0.1)
                        {
                            ReferenceArray gapRefArray = new ReferenceArray();
                            gapRefArray.Append(rightFaces[i]);
                            gapRefArray.Append(leftFaces[i + 1]);

                            XYZ dimLinePosition = rightFacePos + new XYZ(0, 0, offset);
                            Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);

                            _doc.FamilyCreate.NewDimension(_frontView, dimLine, gapRefArray);
                        }
                    }
                }

                ReferenceArray refArrayGeneral = new ReferenceArray();
                refArrayGeneral.Append(leftFaces.First());
                refArrayGeneral.Append(rightFaces.Last());

                double offsetAdjustment = (isExhaust == _isIntakeBelow) ? offset + 1 : offset - 1;
                XYZ dimLinePositionGeneral = GetFaceCenter(leftFaces.First()) + new XYZ(0, 0, offsetAdjustment);

                Line dimLineGeneral = Line.CreateBound(dimLinePositionGeneral, dimLinePositionGeneral + XYZ.BasisX * 10);

                _doc.FamilyCreate.NewDimension(_frontView, dimLineGeneral, refArrayGeneral);

                tx.Commit();

                tx.Start();

                RemoveZeroDimensionsFromFrontView();

                tx.Commit();
            }
        }

        private void RemoveZeroDimensionsFromFrontView()
        {
            List<ElementId> toDelete = new List<ElementId>();

            var dimensions = new FilteredElementCollector(_doc, _frontView.Id)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>();

            foreach (var dimension in dimensions)
            {
                if (dimension.Value != null)
                {
                    double dimValue = dimension.Value.Value;
                    if (dimValue == 0)
                    {
                        toDelete.Add(dimension.Id);
                    }
                }
            }

            if (toDelete.Any())
            {
                _doc.Delete(toDelete);
            }
        }

        private void CreateVerticalDimensions(List<Tuple<Element, VentUnitItem>> intakeElements, List<Tuple<Element, VentUnitItem>> exhaustElements)
        {
            using (Transaction tx = new(_doc, "Creating vertical dimensions"))
            {
                tx.Start();

                var intakeResult = ProcessVerticalAnnotations(intakeElements, true);
                var exhaustResult = ProcessVerticalAnnotations(exhaustElements, false);

                if (intakeResult != null && exhaustResult != null)
                {
                    Reference minZFace = _isIntakeBelow ? intakeResult.Item1 : exhaustResult.Item1;
                    Reference maxZFace = _isIntakeBelow ? exhaustResult.Item1 : intakeResult.Item1;

                    XYZ minDimLinePosition = intakeResult.Item2.X < exhaustResult.Item2.X ? intakeResult.Item2 : exhaustResult.Item2;
                    XYZ finalDimLinePosition = minDimLinePosition - new XYZ(1, 0, 0);

                    CreateDimension(minZFace, maxZFace, finalDimLinePosition);
                }

                tx.Commit();
                tx.Start();

                RemoveZeroDimensionsFromFrontView();

                tx.Commit();
            }
        }

        private Tuple<Reference, XYZ> ProcessVerticalAnnotations(List<Tuple<Element, VentUnitItem>> elements, bool isIntake)
        {
            if (!elements.Any()) return null;

            var leftmostElement = elements.OrderBy(e => GetFaceCenter(GetExtremeFace(e.Item1, false, XYZ.BasisX)).X).FirstOrDefault();
            Reference leftmostFace = leftmostElement != null ? GetExtremeFace(leftmostElement.Item1, false, XYZ.BasisX) : null;

            if (leftmostFace == null)
            {
                TaskDialog.Show("Ошибка", "Не найдена левая грань для размеров.");
                return null;
            }

            if (isIntake)
            {
                var highestElement = elements.OrderByDescending(e => e.Item2.HeightTotal).FirstOrDefault();
                if (highestElement != null)
                {
                    elements.Remove(highestElement);
                }
            }

            var elementWithMaxZDiff = elements
                .Select(e =>
                {
                    var topFace = GetExtremeFace(e.Item1, true, XYZ.BasisZ);
                    var bottomFace = GetExtremeFace(e.Item1, false, XYZ.BasisZ);

                    if (topFace == null || bottomFace == null)
                        return null;

                    return new
                    {
                        Element = e.Item1,
                        TopFace = topFace,
                        BottomFace = bottomFace,
                        ZDifference = GetFaceCenter(topFace).Z - GetFaceCenter(bottomFace).Z
                    };
                })
                .Where(e => e != null)
                .OrderByDescending(e => e.ZDifference)
                .FirstOrDefault();

            Reference targetFace = null;
            XYZ targerDimLinePosition = new XYZ();

            if (elementWithMaxZDiff != null && elementWithMaxZDiff.TopFace != null && elementWithMaxZDiff.BottomFace != null)
            {
                targerDimLinePosition = GetFaceCenter(leftmostFace) - new XYZ(3, 0, 0);
                CreateDimension(elementWithMaxZDiff.BottomFace, elementWithMaxZDiff.TopFace, targerDimLinePosition);
                targetFace = (_isIntakeBelow == isIntake) ? elementWithMaxZDiff.BottomFace : elementWithMaxZDiff.TopFace;
            }

            var flexibleDamperElement = elements.FirstOrDefault(e => e.Item2.Children.Any(child => child.Type.Contains("flexibleDamper")));
            if (flexibleDamperElement != null)
            {
                Options geomOptions = new Options { ComputeReferences = true };
                GeometryElement geomElem = flexibleDamperElement.Item1.get_Geometry(geomOptions);

                List<Edge> allEdges = new List<Edge>();

                foreach (GeometryObject obj in geomElem)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Edge edge in solid.Edges)
                        {
                            allEdges.Add(edge);
                        }
                    }
                }

                if (allEdges.Count >= 2)
                {
                    var edgeZPoints = allEdges
                        .Select(edge => new
                        {
                            Edge = edge,
                            MidPoint = edge.Evaluate(0.5)
                        })
                        .OrderBy(e => e.MidPoint.Z)
                        .ToList();

                    if (edgeZPoints.Count >= 2)
                    {
                        var bottomEdge = edgeZPoints.First();
                        var topEdge = edgeZPoints.Last();

                        Reference ref1 = bottomEdge.Edge.Reference;
                        Reference ref2 = topEdge.Edge.Reference;

                        if (ref1 != null && ref2 != null)
                        {
                            XYZ dimLinePosition = GetFaceCenter(leftmostFace) - new XYZ(2, 0, 0);
                            CreateDimension(ref1, ref2, dimLinePosition);
                        }
                    }
                }
            }
            else
            {
                var elementWithMinZDiff = elements
                    .Select(e => new
                    {
                        Element = e.Item1,
                        TopFace = GetExtremeFace(e.Item1, true, XYZ.BasisZ),
                        BottomFace = GetExtremeFace(e.Item1, false, XYZ.BasisZ),
                        ZDifference = GetFaceCenter(GetExtremeFace(e.Item1, true, XYZ.BasisZ)).Z - GetFaceCenter(GetExtremeFace(e.Item1, false, XYZ.BasisZ)).Z
                    })
                    .Where(e => e.TopFace != null && e.BottomFace != null)
                    .OrderBy(e => e.ZDifference)
                    .FirstOrDefault();

                if (elementWithMinZDiff != null && elementWithMinZDiff.TopFace != null && elementWithMinZDiff.BottomFace != null)
                {
                    XYZ dimLinePosition = GetFaceCenter(leftmostFace) - new XYZ(2, 0, 0);
                    CreateDimension(elementWithMinZDiff.BottomFace, elementWithMinZDiff.TopFace, dimLinePosition);
                }
            }
            return Tuple.Create(targetFace, targerDimLinePosition);
        }

        private Reference GetExtremeFace(Element element, bool getMax, XYZ normal)
        {
            List<Reference> faces = GetParallelFaces(element, normal);
            if (faces.Count == 0) return null;

            return getMax
                ? faces.OrderBy(f => GetFaceCenter(f).Z).LastOrDefault() 
                : faces.OrderBy(f => GetFaceCenter(f).Z).FirstOrDefault(); 
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
                        if (planarFace != null &&
                            (planarFace.FaceNormal.IsAlmostEqualTo(normal) ||
                             planarFace.FaceNormal.IsAlmostEqualTo(-normal)))
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

            List<XYZ> facePoints = new List<XYZ>();

            foreach (EdgeArray edgeArray in face.EdgeLoops)
            {
                foreach (Edge edge in edgeArray)
                {
                    IList<XYZ> edgePoints = edge.Tessellate();
                    facePoints.AddRange(edgePoints);
                }
            }

            if (facePoints.Count == 0) return XYZ.Zero;

            double avgX = facePoints.Average(p => p.X);
            double avgY = facePoints.Average(p => p.Y);
            double avgZ = facePoints.Average(p => p.Z);

            return new XYZ(avgX, avgY, avgZ);
        }

        private void CreateDimension(Reference ref1, Reference ref2, XYZ dimLinePosition)
        {
            Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisZ);

            ReferenceArray refArray = new ReferenceArray();
            refArray.Append(ref1);
            refArray.Append(ref2);

            _doc.FamilyCreate.NewDimension(_frontView, dimLine, refArray);
        }
    }
}
