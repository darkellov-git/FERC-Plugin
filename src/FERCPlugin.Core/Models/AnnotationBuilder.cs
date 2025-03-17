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
        private readonly View _refView;
        private static double _halfHeightIntake;
        private static double _halfHeightExhaust;
        private static double _halfmaxWidth;

        private const double MM_TO_FEET = 0.00328084;

        public AnnotationBuilder(
            Document doc,
            List<Tuple<Element, VentUnitItem>> intakeElements,
            List<Tuple<Element, VentUnitItem>> exhaustElements,
            bool hasUtilizationCross,
            bool isIntakeBelow,
            double maxHeightIntake,
            double maxHeightExhaust,
            double maxWidth)
        {
            _doc = doc;
            _intakeElements = intakeElements;
            _exhaustElements = exhaustElements;
            _hasUtilizationCross = hasUtilizationCross;
            _isIntakeBelow = isIntakeBelow;
            _halfHeightIntake = maxHeightIntake / 2;
            _halfHeightExhaust = maxHeightExhaust / 2;
            _halfmaxWidth = maxWidth / 2;
            _frontView = GetView("Front");
            _refView = GetView("Ref. Level");
        }

        public void AddAnnotations()
        {
            if (_frontView == null || _refView == null)
            {
                TaskDialog.Show("Ошибка", "Вид Front & Ref. Level не найден.");
                return;
            }

            XYZ offset = new XYZ(0, 0, 0);

            CreateHorizontalDimensions(_intakeElements, _isIntakeBelow ? new XYZ(0, 0, -_halfHeightIntake - 1) : new XYZ(0, 0, _halfHeightIntake + 1), false, _frontView);
            CreateHorizontalDimensions(_exhaustElements, _isIntakeBelow || _intakeElements.Count == 0 ? new XYZ(0, 0, _halfHeightExhaust + 1) : new XYZ(0, 0, -_halfHeightExhaust - 1), true, _frontView);
            CreateVerticalDimensions(_intakeElements, _exhaustElements);

            if (_isIntakeBelow || _intakeElements.Count == 0)
            {
                CreateHorizontalDimensions(_exhaustElements, new XYZ(0, _halfmaxWidth + 1, 0), true, _refView);
                CreateTopVerticalDimensions(_exhaustElements);
            }
            if (!_isIntakeBelow || _exhaustElements.Count == 0)
            {
                CreateHorizontalDimensions(_intakeElements, new XYZ(0, _halfmaxWidth + 1, 0), false, _refView);
                CreateTopVerticalDimensions(_intakeElements);
            }

            CreateTextForDisplayElements();
        }

        private View GetView(string viewName)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));
        }

        private void CreateTopHorizontalDimensions(List<Tuple<Element, VentUnitItem>> elements)
        {
            using (Transaction tx = new Transaction(_doc, "Creating top horizontal dimensions"))
            {
                tx.Start();

                if (elements.Count < 2) return;

                List<Tuple<Reference, Reference>> facePairs = new();

                foreach (var (element, _) in elements)
                {
                    List<Reference> xAlignedFaces = GetParallelFaces(element, XYZ.BasisX);
                    if (xAlignedFaces.Count < 2) continue;

                    Reference leftFace = xAlignedFaces.OrderBy(f => GetFaceCenter(f).X).First();
                    Reference rightFace = xAlignedFaces.OrderBy(f => GetFaceCenter(f).X).Last();

                    facePairs.Add(Tuple.Create(leftFace, rightFace));
                }

                if (facePairs.Count < 2) return;

                double dimLineOffset = _halfmaxWidth * 2 + 1;
                XYZ dimLineBasePosition = new XYZ(0, dimLineOffset, 0);

                for (int i = 0; i < facePairs.Count; i++)
                {
                    ReferenceArray refArray = new ReferenceArray();

                    refArray.Append(facePairs[i].Item1);
                    refArray.Append(facePairs[i].Item2);
                    Line dimLine = Line.CreateBound(dimLineBasePosition, dimLineBasePosition + XYZ.BasisX * 10);
                    _doc.FamilyCreate.NewDimension(_refView, dimLine, refArray);

                    if (i < facePairs.Count - 1)
                    {
                        ReferenceArray interElementRefArray = new ReferenceArray();
                        interElementRefArray.Append(facePairs[i].Item2);
                        interElementRefArray.Append(facePairs[i + 1].Item1);
                        Line interElementDimLine = Line.CreateBound(dimLineBasePosition, dimLineBasePosition + XYZ.BasisX * 10);
                        _doc.FamilyCreate.NewDimension(_refView, interElementDimLine, interElementRefArray);
                    }
                }

                tx.Commit();
                tx.Start();

                RemoveZeroDimensionsFromFrontView(_refView);

                tx.Commit();
            }
        }

        private void CreateTopVerticalDimensions(List<Tuple<Element, VentUnitItem>> elements)
        {
            using (Transaction tx = new Transaction(_doc, "Creating top vertical dimensions"))
            {
                tx.Start();

                var allElements = _intakeElements.Concat(_exhaustElements).ToList();
                if (allElements.Count < 2) return;

                Tuple<Element, VentUnitItem> maxWidthElement = null;
                Tuple<Element, VentUnitItem> minWidthElement = null;
                Tuple<Element, VentUnitItem> leftmostElement = null;

                double maxWidth = double.MinValue;
                double minWidth = double.MaxValue;
                double minX = double.MaxValue;

                foreach (var element in allElements)
                {
                    double width = element.Item2.WidthTotal;
                    if (width > maxWidth)
                    {
                        maxWidth = width;
                        maxWidthElement = element;
                    }

                    List<Reference> verticalFaces = GetParallelFaces(element.Item1, XYZ.BasisX);
                    if (verticalFaces.Any())
                    {
                        double elementMinX = verticalFaces.Select(f => GetFaceCenter(f).X).Min();
                        if (elementMinX < minX)
                        {
                            minX = elementMinX;
                            leftmostElement = element;
                        }
                    }
                }

                foreach (var element in allElements)
                {
                    double width = element.Item2.WidthTotal;
                    if (width < minWidth && width > 0 && width < maxWidth)
                    {
                        minWidth = width;
                        minWidthElement = element;
                    }
                }

                if (maxWidthElement == null || minWidthElement == null || leftmostElement == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось найти нужные элементы.");
                    return;
                }

                List<Reference> maxWidthFaces = GetParallelFaces(maxWidthElement.Item1, XYZ.BasisY);
                if (maxWidthFaces.Count < 2) return;

                Reference maxWidthLeftFace = maxWidthFaces.OrderBy(f => GetFaceCenter(f).Y).First();
                Reference maxWidthRightFace = maxWidthFaces.OrderBy(f => GetFaceCenter(f).Y).Last();

                List<Reference> minWidthFaces = GetParallelFaces(minWidthElement.Item1, XYZ.BasisY);
                if (minWidthFaces.Count < 2) return;

                Reference minWidthLeftFace = minWidthFaces.OrderBy(f => GetFaceCenter(f).Y).First();
                Reference minWidthRightFace = minWidthFaces.OrderBy(f => GetFaceCenter(f).Y).Last();

                XYZ leftmostFacePosition = new XYZ(minX, 0, 0);

                ReferenceArray maxWidthRefArray = new ReferenceArray();
                maxWidthRefArray.Append(maxWidthLeftFace);
                maxWidthRefArray.Append(maxWidthRightFace);
                Line maxWidthDimLine = Line.CreateBound(leftmostFacePosition + new XYZ(-2, 0, 0), leftmostFacePosition + new XYZ(-2, 10, 0));
                _doc.FamilyCreate.NewDimension(_refView, maxWidthDimLine, maxWidthRefArray);

                ReferenceArray minWidthRefArray = new ReferenceArray();
                minWidthRefArray.Append(minWidthLeftFace);
                minWidthRefArray.Append(minWidthRightFace);
                Line minWidthDimLine = Line.CreateBound(leftmostFacePosition + new XYZ(-1, 0, 0), leftmostFacePosition + new XYZ(-1, 10, 0));
                _doc.FamilyCreate.NewDimension(_refView, minWidthDimLine, minWidthRefArray);

                tx.Commit();
                tx.Start();

                RemoveZeroDimensionsFromFrontView(_refView);

                tx.Commit();
            }
        }

        private void CreateHorizontalDimensions(List<Tuple<Element, VentUnitItem>> elements, XYZ offset, bool isExhaust, View view)
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
                        XYZ dimLinePosition = GetFaceCenter(leftFaces.First()) + offset;
                        Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);
                        _doc.FamilyCreate.NewDimension(_frontView, dimLine, refArray);
                    }
                }

                if (isExhaust & _intakeElements.Count > 0)
                {
                    for (int i = 0; i < leftFaces.Count; i++)
                    {
                        ReferenceArray singleRefArray = new ReferenceArray();
                        singleRefArray.Append(leftFaces[i]);
                        singleRefArray.Append(rightFaces[i]);

                        XYZ dimLinePosition = GetFaceCenter(leftFaces[i]) + offset;
                        Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);

                        _doc.FamilyCreate.NewDimension(view, dimLine, singleRefArray);
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

                            XYZ dimLinePosition = rightFacePos + offset;
                            Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);

                            _doc.FamilyCreate.NewDimension(view, dimLine, gapRefArray);
                        }
                    }
                }

                if (isExhaust & _intakeElements.Count == 0)
                {
                    for (int i = 0; i < leftFaces.Count; i++)
                    {
                        ReferenceArray singleRefArray = new ReferenceArray();
                        singleRefArray.Append(leftFaces[i]);
                        singleRefArray.Append(rightFaces[i]);

                        XYZ dimLinePosition = GetFaceCenter(leftFaces[i]) + offset + new XYZ(0, 0, _halfHeightIntake);
                        Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);

                        _doc.FamilyCreate.NewDimension(view, dimLine, singleRefArray);
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

                            XYZ dimLinePosition = rightFacePos + offset + new XYZ(0, 0, _halfHeightIntake);
                            Line dimLine = Line.CreateBound(dimLinePosition, dimLinePosition + XYZ.BasisX * 10);

                            _doc.FamilyCreate.NewDimension(view, dimLine, gapRefArray);
                        }
                    }
                }

                ReferenceArray refArrayGeneral = new ReferenceArray();
                refArrayGeneral.Append(leftFaces.First());
                refArrayGeneral.Append(rightFaces.Last());
                XYZ offsetAdjustment = new XYZ(0, 0, 0);
                XYZ dimLinePositionGeneral = new XYZ(0, 0, 0);


                offsetAdjustment = (isExhaust == _isIntakeBelow) ? offset + new XYZ(0, 0, 1) : offset - new XYZ(0, 0, 1);
                dimLinePositionGeneral = GetFaceCenter(leftFaces.First()) + offsetAdjustment + new XYZ(0, 1, 0);

                if (isExhaust && _intakeElements.Count == 0)
                {
                    dimLinePositionGeneral = GetFaceCenter(leftFaces.First()) + offset + new XYZ(0, 0, _halfHeightIntake + 1);
                }

                Line dimLineGeneral = Line.CreateBound(dimLinePositionGeneral, dimLinePositionGeneral + XYZ.BasisX * 10);

                _doc.FamilyCreate.NewDimension(view, dimLineGeneral, refArrayGeneral);

                tx.Commit();

                tx.Start();

                RemoveZeroDimensionsFromFrontView(view);

                tx.Commit();
            }
        }

        private void RemoveZeroDimensionsFromFrontView(View view)
        {
            List<ElementId> toDelete = new List<ElementId>();

            var dimensions = new FilteredElementCollector(_doc, view.Id)
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

                RemoveZeroDimensionsFromFrontView(_frontView);

                tx.Commit();
            }
        }

        private Tuple<Reference, XYZ> ProcessVerticalAnnotations(List<Tuple<Element, VentUnitItem>> elements, bool isIntake)
        {
            List<Tuple<Element, VentUnitItem>> elementsCopy = DeepCopyElements(elements);

            if (!elementsCopy.Any()) return null;

            var leftmostElement = elementsCopy.OrderBy(e => GetFaceCenter(GetExtremeFace(e.Item1, false, XYZ.BasisX)).X).FirstOrDefault();
            Reference leftmostFace = leftmostElement != null ? GetExtremeFace(leftmostElement.Item1, false, XYZ.BasisX) : null;

            if (leftmostFace == null)
            {
                TaskDialog.Show("Ошибка", "Не найдена левая грань для размеров.");
                return null;
            }

            if (isIntake)
            {
                var highestElement = elementsCopy.OrderByDescending(e => e.Item2.HeightTotal).FirstOrDefault();
                if (highestElement != null)
                {
                    elementsCopy.Remove(highestElement);
                }
            }

            var elementWithMaxZDiff = elementsCopy
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

            var flexibleDamperElement = elementsCopy.FirstOrDefault(e => e.Item2.Children.Any(child => child.Type.Contains("flexibleDamper")));
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
                var elementWithMinZDiff = elementsCopy
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

        private void CreateTextForDisplayElements()
        {
            using (Transaction tx = new Transaction(_doc, "Create Filled Regions"))
            {
                tx.Start();

                List<Tuple<Element, VentUnitItem>> allElements = _intakeElements.Concat(_exhaustElements).ToList();
                List<FamilyInstance> createdRegions = new List<FamilyInstance>();

                foreach (var (element, ventUnitItem) in allElements)
                {
                    if (ventUnitItem.DisplayIndex == -1) continue;

                    List<Reference> yAlignedFaces = GetParallelFaces(element, XYZ.BasisY);
                    if (!yAlignedFaces.Any()) continue;

                    Reference targetFaceRef = yAlignedFaces.Count > 1 ? yAlignedFaces[1] : yAlignedFaces[0];
                    Face targetFace = element.GetGeometryObjectFromReference(targetFaceRef) as Face;
                    if (targetFace == null) continue;

                    List<CurveLoop> contours = targetFace.GetEdgesAsCurveLoops().ToList();
                    if (contours.Count == 0) continue;

                    XYZ centerPoint = GetCentroidPoint(contours);
                    XYZ topLeftPoint = GetTopLeftPoint(contours);
                    double width = ventUnitItem.LengthTotal;
                    double height = ventUnitItem.HeightTotal;

                    AddDisplayIndexText(topLeftPoint, ventUnitItem.DisplayIndex ?? -1);
                }

                _frontView.DisplayStyle = DisplayStyle.FlatColors;

                _frontView.DetailLevel = ViewDetailLevel.Fine;

                _refView.DisplayStyle = DisplayStyle.FlatColors;

                _refView.DetailLevel = ViewDetailLevel.Fine;

                tx.Commit();
            }
        }

        private XYZ GetTopLeftPoint(List<CurveLoop> contours)
        {
            if (contours == null || contours.Count == 0)
                return XYZ.Zero;

            return contours
                .SelectMany(loop => loop)
                .Select(curve => curve.GetEndPoint(0))
                .OrderBy(p => p.X)
                .ThenByDescending(p => p.Z)
                .FirstOrDefault() ?? XYZ.Zero;
        }

        private void AddDisplayIndexText(XYZ location, int displayIndex)
        {
            TextNoteType textType = GetOrCreateTextNoteType();

            TextNoteOptions options = new TextNoteOptions
            {
                TypeId = textType.Id
            };

            XYZ newLocation = new XYZ(location.X + 0.25, location.Y, location.Z - 0.3);

            TextNote.Create(_doc, _frontView.Id, newLocation, displayIndex.ToString(), options);
        }

        private TextNoteType GetOrCreateTextNoteType()
        {
            TextNoteType textNoteType = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(t => t.Name == "3 mm");

            if (textNoteType == null)
            {
                TextNoteType defaultTextType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();

                if (defaultTextType != null)
                {
                    textNoteType = defaultTextType.Duplicate("3 mm") as TextNoteType;
                    if (textNoteType != null)
                    {
                        textNoteType.LookupParameter("Text Size")?.Set(3 * MM_TO_FEET);
                        textNoteType.LookupParameter("Bold")?.Set(1);
                        textNoteType.LookupParameter("Color")?.Set(0 | 0 | 255);
                        textNoteType.LookupParameter("Background")?.Set(1);
                        textNoteType.LookupParameter("Show Border")?.Set(1);
                    }
                }
            }
            return textNoteType;
        }

        private XYZ GetCentroidPoint(List<CurveLoop> contours)
        {
            List<XYZ> points = contours
                .SelectMany(loop => loop)
                .SelectMany(curve => new List<XYZ> { curve.GetEndPoint(0), curve.GetEndPoint(1) })
                .Distinct()
                .ToList();

            if (!points.Any()) return XYZ.Zero;

            double avgX = points.Average(p => p.X);
            double avgY = points.Average(p => p.Y);
            double avgZ = points.Average(p => p.Z);

            return new XYZ(avgX, avgY, avgZ);
        }

        private List<Tuple<Element, VentUnitItem>> DeepCopyElements(List<Tuple<Element, VentUnitItem>> elements)
        {
            return elements.Select(t => Tuple.Create(t.Item1, new VentUnitItem
            {
                Id = t.Item2.Id,
                Category = t.Item2.Category,
                DisplayIndex = t.Item2.DisplayIndex,
                LengthTotal = t.Item2.LengthTotal,
                HeightTotal = t.Item2.HeightTotal,
                WidthTotal = t.Item2.WidthTotal,
                TopPanels = t.Item2.TopPanels.Select(p => new VentUnitPanel { SizeY = p.SizeY, SizesX = new List<double>(p.SizesX) }).ToList(),
                FloorPanels = t.Item2.FloorPanels.Select(p => new VentUnitPanel { SizeY = p.SizeY, SizesX = new List<double>(p.SizesX) }).ToList(),
                BackPanels = t.Item2.BackPanels.Select(p => new VentUnitPanel { SizeY = p.SizeY, SizesX = new List<double>(p.SizesX) }).ToList(),
                Children = t.Item2.Children.Select(c => new VentUnitChild
                {
                    Id = c.Id,
                    Type = c.Type,
                    LengthTotal = c.LengthTotal,
                    HeightTotal = c.HeightTotal,
                    WidthTotal = c.WidthTotal,
                    ServicePanels = c.ServicePanels.Select(p => new VentUnitPanel { SizeY = p.SizeY, SizesX = new List<double>(p.SizesX) }).ToList(),
                    Pipes = c.Pipes.Select(p => new VentUnitPipe { X = p.X, Y = p.Y, D = p.D }).ToList(),
                    Window = c.Window != null ? new VentUnitWindow { X = c.Window.X, Y = c.Window.Y, D = c.Window.D } : null
                }).ToList(),
                CutInfo = new UtilizerUnitCutInfo
                {
                    HasLeftCut = t.Item2.CutInfo.HasLeftCut,
                    HasRightCut = t.Item2.CutInfo.HasRightCut,
                    CutSize = t.Item2.CutInfo.CutSize
                }
            })).ToList();
        }
    }
}
