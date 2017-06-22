﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

using SpatialSlur.SlurCore;
using SpatialSlur.SlurData;
using SpatialSlur.SlurField;
using SpatialSlur.SlurMesh;

using SpatialSlur.SlurRhino;
using SpatialSlur.SlurRhino.Remesher;

using static SpatialSlur.SlurCore.SlurMath;


/*
 * Notes
 * 
 * Curvature adaptive dynamic remeshing based on implemetation described in
 * https://nccastaff.bournemouth.ac.uk/jmacey/MastersProjects/MSc15/08Tanja/report.pdf
 * 
 * References
 * http://graphics.stanford.edu/courses/cs468-12-spring/LectureSlides/13_Remeshing1.pdf
 */

namespace SpatialSlur.SlurRhino.GraphGrowth
{
    using V = HeGraphGG.V;
    using E = HeGraphGG.E;


    /// <summary>
    /// 
    /// </summary>
    public class GraphGrower
    {
        #region Static


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TV"></typeparam>
        /// <typeparam name="TE"></typeparam>
        /// <typeparam name="TF"></typeparam>
        /// <param name="mesh"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static GraphGrower Create<TV, TE>(HeGraph<TV, TE> graph, IFeature target, IEnumerable<IFeature> features, double tolerance = 1.0e-4)
            where TV : HeVertex<TV, TE>, IVertex3d
            where TE : Halfedge<TV, TE>
        {
            var copy = HeGraphGG.Factory.CreateCopy(graph, (v0, v1) => v0.Position = v1.Position, delegate { });
            return new GraphGrower(copy, target, features, tolerance);
        }


        #endregion
        

        private const double TargetBinScale = 2.0;
        private const double MinBinScale = TargetBinScale * 0.5;
        private const double MaxBinScale = TargetBinScale * 2.0;
        private const double GridPadding = 1.0e-4;

        //
        // simulation mesh
        //

        private HeGraph<V, E> _graph;
        private HeElementList<V> _verts;
        private HeElementList<E> _hedges;
        private SpatialGrid3d<V> _grid;

        //
        // constraint objects
        //

        private IFeature _target;
        private List<IFeature> _features;
        private IField3d<double> _lengthField;

        //
        // simulation settings
        //

        private GraphGrowerSettings _settings;
        private int _stepCount = 0;
        private int _vertTag = int.MinValue;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public GraphGrower(HeGraph<V, E> graph, IFeature target, IEnumerable<IFeature> features, double tolerance = 1.0e-4)
        {
            _graph = graph;
            _verts = _graph.Vertices;
            _hedges = _graph.Halfedges;

            // initialize features
            _target = target;
            InitFeatures(features, tolerance);

            _settings = new GraphGrowerSettings();
            _stepCount = 0;
        }


        /// <summary>
        /// 
        /// </summary>
        private void InitFeatures(IEnumerable<IFeature> features, double tolerance)
        {
            _features = new List<IFeature>();
            var tolSqr = tolerance * tolerance;

            // create features
            foreach (var f in features)
            {
                int index = _features.Count;
                _features.Add(f);

                // if vertex is close enough, assign feature
                foreach (var v in _verts)
                {
                    var p = v.Position;
                    if (p.SquareDistanceTo(f.ClosestPoint(p)) < tolSqr)
                        v.FeatureIndex = index;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public HeGraph<V, E> Graph
        {
            get { return _graph; }
        }


        /// <summary>
        /// 
        /// </summary>
        public IField3d<double> EdgeLengthField
        {
            get { return _lengthField; }
            set { _lengthField = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        public GraphGrowerSettings Settings
        {
            get { return _settings; }
            set { _settings = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        public int StepCount
        {
            get { return _stepCount; }
        }


        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public void Step()
        {
            for (int i = 0; i < _settings.SubSteps; i++)
            {
                if (++_stepCount % _settings.RefineFrequency == 0)
                    Refine();
  
                CalculateForces();
                UpdateVertices();
            }
        }


        #region Dynamics


        /// <summary>
        /// Calculates all forces in the system
        /// </summary>
        /// <returns></returns>
        private void CalculateForces()
        {
            PullToFeatures(_settings.FeatureWeight);
            LaplacianFair(_settings.SmoothWeight);
            //LaplacianFair2(_settings.SmoothWeight);
            SphereCollide(_settings.CollideRadius, _settings.CollideWeight);
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="weight"></param>
        /// <returns></returns>
        private void PullToFeatures(double weight)
        {
            Parallel.ForEach(Partitioner.Create(0, _verts.Count), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var v = _verts[i];
                    if (v.IsRemoved || v.FeatureIndex == -1) continue;

                    var p = v.Position;
                    int fi = v.FeatureIndex;

                    if (fi == -1) continue;
                    v.MoveSum += (_features[fi].ClosestPoint(p) - p) * weight;
                    v.WeightSum += weight;

                    /*
                    if (fi == -1)
                    {
                        if (_target == null) continue;

                        v.MoveSum += (_target.ClosestPoint(p) - p);
                        v.WeightSum += 1.0; // default weight of 1.0 for target
                    }
                    else
                    {
                        v.MoveSum += (_features[fi].ClosestPoint(p) - p) * weight;
                        v.WeightSum += weight;
                    }
                    */
                }
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="weight"></param>
        private void LaplacianFair(double weight)
        {
            for(int i = 0; i < _verts.Count; i++)
            {
                var v0 = _verts[i];
                if (v0.IsRemoved) continue;
                
                // calculate graph laplacian
                var sum = new Vec3d();
                var count = 0;

                foreach(var v1 in v0.ConnectedVertices)
                {
                    sum += v1.Position;
                    count++;
                }

                double t = 1.0 / count;
                var move = sum * t - v0.Position;

                // apply to central vertex
                v0.MoveSum += move * weight;
                v0.WeightSum += weight;
  
                // distribute negated to neighbours
                move *= -t;
                foreach (var v1 in v0.ConnectedVertices)
                {
                    v1.MoveSum += move * weight;
                    v1.WeightSum += weight;
                }
            }
        }


        /*
        /// <summary>
        ///
        /// </summary>
        /// <param name="weight"></param>
        /// <returns></returns>
        private void LaplacianFair2(double weight)
        {
            _graph.GetVertexLaplacian(v => v.Position, (v, n) => v.Normal = n, true); // precalculate position laplacian for each vertex

            Parallel.ForEach(Partitioner.Create(0, _verts.Count), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var v0 = _verts[i];
                    if (v0.IsRemoved) continue;

                    var move = v0.Normal;
                    int count = 1;

                    foreach (var v1 in v0.ConnectedVertices)
                    {
                        move -= v1.Normal;
                        count++;
                    }

                    v0.MoveSum += move * (0.5 * weight / count);
                    v0.WeightSum += weight;
                }
            });
        }
        */


        /// <summary>
        /// 
        /// </summary>
        /// <param name="weight"></param>
        private void SphereCollide(double radius, double weight)
        {
            UpdateGrid(radius);

            var rad2 = radius * 2.0;
            var rad2Sqr = rad2 * rad2;

            // insert vertices
            foreach (var v in _verts)
                _grid.Insert(v.Position, v);

            // search from each vertex and handle collisions
            Parallel.ForEach(Partitioner.Create(0, _verts.Count), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var v0 = _verts[i];
                    var p0 = v0.Position;
                    var moveSum = new Vec3d();

                    _grid.Search(new Domain3d(p0, rad2), v1 =>
                    {
                        if (v0 == v1) return true;

                        var move = v1.Position - p0;
                        var d = move.SquareLength;

                        if (d < rad2Sqr && d > 0.0)
                            moveSum += move * ((1.0 - rad2 / Math.Sqrt(d)) * 0.5);

                        return true;
                    });

                    v0.MoveSum += moveSum * weight;
                    v0.WeightSum += weight;
                }
            });

            _grid.Clear();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="particles"></param>
        private void UpdateGrid(double radius)
        {
            // recalculate domain
            Domain3d d = new Domain3d(_verts.Select(v => v.Position));
            d.Expand(GridPadding);

            // lazy instantiation
            if(_grid == null)
            {
                InitGrid(d, radius * TargetBinScale);
                return;
            }

            // rebuild grid if bins are too large or too small in any one dimension
            _grid.Domain = d;
            double t0 = radius * MaxBinScale;
            double t1 = radius * MinBinScale;

            if (!(Contains(_grid.BinScaleX, t0, t1) && Contains(_grid.BinScaleY, t0, t1) && Contains(_grid.BinScaleZ, t0, t1)))
                InitGrid(d, radius * TargetBinScale);
        }


        /// <summary>
        /// 
        /// </summary>
        private void InitGrid(Domain3d domain, double binScale)
        {
            int nx = (int)Math.Ceiling(domain.X.Span / binScale);
            int ny = (int)Math.Ceiling(domain.Y.Span / binScale);
            int nz = (int)Math.Ceiling(domain.Z.Span / binScale);
            _grid = new SpatialGrid3d<V>(domain, nx, ny, nz);
        }


        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private void UpdateVertices()
        {
            double timeStep = _settings.TimeStep;
            double damping = _settings.Damping;

            Parallel.ForEach(Partitioner.Create(0, _verts.Count), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var v = _verts[i];
                    v.Velocity *= damping;

                    double w = v.WeightSum;
                    if (w > 0.0) v.Velocity += v.MoveSum * (timeStep / w);
                    v.Position += v.Velocity * timeStep;

                    // snap to target
                    if (_target != null)
                        v.Position = _target.ClosestPoint(v.Position);
  
                    v.MoveSum = new Vec3d();
                    v.WeightSum = 0.0;
                }
            });
        }


        #endregion


        #region Topology


        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private void Refine()
        {
            UpdateMaxLengths(true);
            SplitEdges(_hedges.Count);
        }


        /// <summary>
        /// Splits long edges
        /// </summary>
        private void SplitEdges(int count)
        {
            _vertTag++;

            for (int i = 0; i < count; i += 2)
            {
                var he = _hedges[i];
                if (he.IsRemoved) continue;

                var v0 = he.Start;
                var v1 = he.End;
                
                var p0 = v0.Position;
                var p1 = v1.Position;

                // split edge if length exceeds max
                if (p0.SquareDistanceTo(p1) > he.MaxLength * he.MaxLength)
                {
                    var v2 = _graph.SplitEdge(he).Start;

                    // set attributes of new vertex
                    v2.Position = (v0.Position + v1.Position) * 0.5;
                    v2.FeatureIndex = Math.Min(v0.FeatureIndex, v1.FeatureIndex);
                }
            }
        }


        #endregion


        #region Attributes


        /// <summary>
        /// 
        /// </summary>
        void UpdateMaxLengths(bool parallel = false)
        {
            var lengthRange = _settings.LengthRange;

            // set length targets to default if no field
            if (_lengthField == null)
            {
                var min = lengthRange.Min;

                for (int i = 0; i < _hedges.Count; i += 2)
                    _hedges[i].MaxLength = min;

                return;
            }

            // evaluate field
            Action<Tuple<int, int>> body = range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var he = _hedges[i << 1];
                    if (he.IsRemoved) continue;

                    var p = (he.Start.Position + he.End.Position) * 0.5;
                    he.MaxLength = lengthRange.Evaluate(_lengthField.ValueAt(p));
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, _hedges.Count >> 1), body);
            else
                body(Tuple.Create(0, _hedges.Count >> 1));
        }


        #endregion
    }
}
