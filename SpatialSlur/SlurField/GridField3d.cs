﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

using SpatialSlur.SlurCore;

using static SpatialSlur.SlurField.FieldUtil;

/*
 * Notes
 */

namespace SpatialSlur.SlurField
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public abstract class Grid3d
    {
        private Domain3d _domain;
        private double _x0, _y0, _z0; // cached for convenience
        private double _dx, _dy, _dz;
        private double _dxInv, _dyInv, _dzInv; // cached to avoid uneccesary divs
        private readonly int _nx, _ny, _nz, _nxy, _n;

        private SampleMode _sampleMode;
        private WrapMode _wrapModeX, _wrapModeY, _wrapModeZ;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="countX"></param>
        /// <param name="countY"></param>
        /// <param name="countZ"></param>
        private Grid3d(int countX, int countY, int countZ)
        {
            if (countX < 1 || countY < 1 || countZ < 1)
                throw new System.ArgumentException("The field must have at least 1 value in each dimension.");

            _nx = countX;
            _ny = countY;
            _nz = countZ;
            _nxy = _nx * _ny;
            _n = _nxy * _nz;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="countX"></param>
        /// <param name="countY"></param>
        /// <param name="countZ"></param>
        protected Grid3d(Domain3d domain, int countX, int countY, int countZ)
            : this(countX, countY, countZ)
        {
            Domain = domain;
            _sampleMode = SampleMode.Linear;
            WrapModeX = WrapModeY = WrapModeZ = WrapMode.Clamp;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="countX"></param>
        /// <param name="countY"></param>
        /// <param name="countZ"></param>
        /// <param name="sampleMode"></param>
        /// <param name="wrapMode"></param>
        protected Grid3d(Domain3d domain, int countX, int countY, int countZ, SampleMode sampleMode, WrapMode wrapMode)
            : this(countX, countY, countZ)
        {
            Domain = domain;
            _sampleMode = sampleMode;
            WrapModeX = WrapModeY = WrapModeZ = wrapMode;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="countX"></param>
        /// <param name="countY"></param>
        /// <param name="countZ"></param>
        /// <param name="sampleMode"></param>
        /// <param name="wrapModeX"></param>
        /// <param name="wrapModeY"></param>
        /// <param name="wrapModeZ"></param>
        protected Grid3d(Domain3d domain, int countX, int countY, int countZ, SampleMode sampleMode, WrapMode wrapModeX, WrapMode wrapModeY, WrapMode wrapModeZ)
            : this(countX, countY, countZ)
        {
            Domain = domain;
            _sampleMode = sampleMode;
            WrapModeX = wrapModeX;
            WrapModeY = wrapModeY;
            WrapModeZ = wrapModeZ;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        protected Grid3d(Grid3d other)
        {
            _nx = other._nx;
            _ny = other._ny;
            _nz = other._nz;
            _nxy = other._nxy;
            _n = other._n;

            Domain = other._domain;
            _sampleMode = other._sampleMode;
            WrapModeX = other._wrapModeX;
            WrapModeY = other._wrapModeY;
            WrapModeZ = other._wrapModeZ;
        }


        /// <summary>
        /// Gets/sets the domain of the field.
        /// </summary>
        public Domain3d Domain
        {
            get { return _domain; }
            set
            {
                if (!value.IsValid)
                    throw new System.ArgumentException("The given domain must be valid.");

                _domain = value;
                OnDomainChange();
            }
        }


        /// <summary>
        /// Returns the number of values in the field.
        /// </summary>
        public int Count
        {
            get { return _n; }
        }


        /// <summary>
        /// Returns the number of values in the x direction.
        /// </summary>
        public int CountX
        {
            get { return _nx; }
        }


        /// <summary>
        /// Returns the number of values in the y direction.
        /// </summary>
        public int CountY
        {
            get { return _ny; }
        }


        /// <summary>
        /// Returns the number of values in the z direction.
        /// </summary>
        public int CountZ
        {
            get { return _nz; }
        }


        /// <summary>
        /// Returns the number of values in a single xy layer.
        /// </summary>
        public int CountXY
        {
            get { return _nxy; }
        }


        /// <summary>
        /// Returns the distance between values in the x direction.
        /// </summary>
        public double ScaleX
        {
            get { return _dx; }
        }


        /// <summary>
        /// Returns the distance between values in the y direction.
        /// </summary>
        public double ScaleY
        {
            get { return _dy; }
        }


        /// <summary>
        /// Returns the distance between values in the z direction.
        /// </summary>
        public double ScaleZ
        {
            get { return _dz; }
        }


        /// <summary>
        /// 
        /// </summary>
        public SampleMode SampleMode
        {
            get { return _sampleMode; }
            set { _sampleMode = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        public WrapMode WrapModeX
        {
            get { return _wrapModeX; }
            set { _wrapModeX = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        public WrapMode WrapModeY
        {
            get { return _wrapModeY; }
            set { _wrapModeY = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        public WrapMode WrapModeZ
        {
            get { return _wrapModeZ; }
            set { _wrapModeZ = value; }
        }


        /// <summary>
        /// This is called after any changes to the field's domain.
        /// </summary>
        protected void OnDomainChange()
        {
            _x0 = _domain.X.T0;
            _y0 = _domain.Y.T0;
            _z0 = _domain.Z.T0;

            _dx = _domain.X.Span / (_nx - 1);
            _dy = _domain.Y.Span / (_ny - 1);
            _dz = _domain.Z.Span / (_nz - 1);

            _dxInv = 1.0 / _dx;
            _dyInv = 1.0 / _dy;
            _dzInv = 1.0 / _dz;
        }


        /// <summary>
        /// Returns coordinates of all values in the field.
        /// </summary>
        /// <param name="parallel"></param>
        /// <returns></returns>
        public Vec3d[] GetCoordinates(bool parallel = false)
        {
            Vec3d[] result = new Vec3d[_n];
            GetCoordinates(result, parallel);
            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        /// <param name="parallel"></param>
        public void GetCoordinates(Vec3d[] result, bool parallel = false)
        {
            Action<Tuple<int, int>> func = range =>
             {
                 (int i, int j, int k) = IndicesAt(range.Item1);

                 for (int index = range.Item1; index < range.Item2; index++, i++)
                 {
                     if (i == _nx) { j++; i = 0; }
                     if (j == _ny) { k++; j = 0; }
                     result[index] = CoordinateAt(i, j, k);
                 }
             };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, _n), func);
            else
                func(Tuple.Create(0, _n));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public Vec3d CoordinateAt(int i, int j, int k)
        {
            return new Vec3d(
                i * _dx + _x0,
                j * _dy + _y0,
                k * _dz + _z0);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vec3d CoordinateAt(int index)
        {
            (int i, int j, int k) = IndicesAt(index);
            return CoordinateAt(i, j, k);
        }


        /// <summary>
        /// Returns true if the field has the same number of values in each dimension as another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool ResolutionEquals(Grid3d other)
        {
            return (_nx == other._nx && _ny == other._ny && _nz == other._nz);
        }


        /// <summary>
        /// Applies a wrap function to the given index based on the current wrap mode.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public int WrapX(int i)
        {
            switch (_wrapModeX)
            {
                case (WrapMode.Clamp):
                    return Clamp(i, _nx);
                case (WrapMode.Repeat):
                    return Repeat(i, _nx);
                case (WrapMode.MirrorRepeat):
                    return MirrorRepeat(i, _nx);
                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        /// Applies a wrap function to the given index based on the current wrap mode.
        /// </summary>
        /// <param name="j"></param>
        /// <returns></returns>
        public int WrapY(int j)
        {
            switch (_wrapModeY)
            {
                case (WrapMode.Clamp):
                    return Clamp(j, _ny);
                case (WrapMode.Repeat):
                    return Repeat(j, _ny);
                case (WrapMode.MirrorRepeat):
                    return MirrorRepeat(j, _ny);
                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        /// Applies a wrap function to the given index based on the current wrap mode.
        /// </summary>
        /// <param name="k"></param>
        /// <returns></returns>
        public int WrapZ(int k)
        {
            switch (_wrapModeZ)
            {
                case (WrapMode.Clamp):
                    return Clamp(k, _nz);
                case (WrapMode.Repeat):
                    return Repeat(k, _nz);
                case (WrapMode.MirrorRepeat):
                    return MirrorRepeat(k, _nz);
                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        /// Flattens a 3 dimensional index into a 1 dimensional index.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public int IndexAt(int i, int j, int k)
        {
            return FlattenIndices(WrapX(i), WrapY(j), WrapZ(k), _nx, _nxy);
        }


        /// <summary>
        /// Flattens a 3 dimensional index into a 1 dimensional index.
        /// Assumes the given indices are within the valid range.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public int IndexAtUnchecked(int i, int j, int k)
        {
            return FlattenIndices(i, j, k, _nx, _nxy);
        }


        /// <summary>
        /// Returns the index of the nearest value to the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public int IndexAt(Vec3d point)
        {
            (int i, int j, int k) = IndicesAt(point);
            return FlattenIndices(WrapX(i), WrapY(j), WrapZ(k), _nx, _nxy);
        }


        /// <summary>
        /// Returns the index of the nearest value to the given point.
        /// Assumes the point is inside the field domain.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public int IndexAtUnchecked(Vec3d point)
        {
            (int i, int j, int k) = IndicesAt(point);
            return FlattenIndices(i, j, k, _nx, _nxy);
        }


        /// <summary>
        /// Expands a 1 dimensional index into a 3 dimensional index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public (int, int, int) IndicesAt(int index)
        {
            return ExpandIndex(index, _nx, _nxy);
        }


        /// <summary>
        /// Expands a 1 dimensional index into a 3 dimensional index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        public void IndicesAt(int index, out int i, out int j, out int k)
        {
            (i, j, k) = ExpandIndex(index, _nx, _nxy);
        }


        /// <summary>
        /// Returns the indices of the nearest value to the given point.
        /// </summary>
        /// <param name="point"></param>
        public (int, int, int) IndicesAt(Vec3d point)
        {
            return (
                (int)Math.Round((point.X - _x0) * _dxInv),
                (int)Math.Round((point.Y - _y0) * _dyInv),
                (int)Math.Round((point.Z - _z0) * _dzInv));
        }


        /// <summary>
        /// Returns the indices of the nearest value to the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        public void IndicesAt(Vec3d point, out int i, out int j, out int k)
        {
            (i, j, k) = IndicesAt(point);
        }


        /// <summary>
        /// Returns a grid point at the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public GridPoint3d GridPointAt(Vec3d point)
        {
            GridPoint3d result = new GridPoint3d();
            GridPointAt(point, result);
            return result;
        }


        /// <summary>
        /// Returns a grid point at the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="result"></param>
        public void GridPointAt(Vec3d point, GridPoint3d result)
        {
            (double u, double v, double w) = Fract(point, out int i0, out int j0, out int k0);
            result.SetWeights(u, v, w);

            int i1 = WrapX(i0 + 1);
            int j1 = WrapY(j0 + 1) * _nx;
            int k1 = WrapZ(k0 + 1) * _nxy;

            i0 = WrapX(i0);
            j0 = WrapY(j0) * _nx;
            k0 = WrapZ(k0) * _nxy;

            var corners = result.Corners;
            corners[0] = i0 + j0 + k0;
            corners[1] = i1 + j0 + k0;
            corners[2] = i0 + j1 + k0;
            corners[3] = i1 + j1 + k0;
            corners[4] = i0 + j0 + k1;
            corners[5] = i1 + j0 + k1;
            corners[6] = i0 + j1 + k1;
            corners[7] = i1 + j1 + k1;
        }


        /// <summary>
        /// Returns a grid point at the given point.
        /// Assumes the given point is inside the field domain.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public GridPoint3d GridPointAtUnchecked(Vec3d point)
        {
            GridPoint3d result = new GridPoint3d();
            GridPointAtUnchecked(point, result);
            return result;
        }


        /// <summary>
        /// Returns a grid point at the given point.
        /// Assumes the point is inside the field's domain.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="result"></param>
        public void GridPointAtUnchecked(Vec3d point, GridPoint3d result)
        {
            (double u, double v, double w) = Fract(point, out int i0, out int j0, out int k0);
            result.SetWeights(u, v, w);

            j0 *= _nx;
            k0 *= _nxy;
            int i1 = i0 + 1;
            int j1 = j0 + _nx;
            int k1 = k0 + _nxy;

            var corners = result.Corners;
            corners[0] = i0 + j0 + k0;
            corners[1] = i1 + j0 + k0;
            corners[2] = i0 + j1 + k0;
            corners[3] = i1 + j1 + k0;
            corners[4] = i0 + j0 + k1;
            corners[5] = i1 + j0 + k1;
            corners[6] = i0 + j1 + k1;
            corners[7] = i1 + j1 + k1;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        protected (double, double, double) Fract(Vec3d point, out int i, out int j, out int k)
        {
            return (
            SlurMath.Fract((point.X - _x0) * _dxInv, out i),
            SlurMath.Fract((point.Y - _y0) * _dyInv, out j),
            SlurMath.Fract((point.Z - _z0) * _dzInv, out k)
            );
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class GridField3d<T> : Grid3d, IField2d<T>, IField3d<T>, IDiscreteField<T>
        where T : struct
    {
        #region Static


        /// <summary>
        /// 
        /// </summary>
        /// <param name="field"></param>
        /// <param name="path"></param>
        /// <param name="mapper"></param>
        public static void SaveAsImageStack(GridField3d<T> field, string path, Func<T, Color> mapper)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            Parallel.For(0, field.CountZ, z =>
            {
                using (Bitmap bmp = new Bitmap(field.CountX, field.CountY, PixelFormat.Format32bppArgb))
                {
                    FieldIO.WriteToImage(field, z, bmp, mapper);
                    bmp.Save(String.Format(@"{0}\{1}_{2}{3}", dir, name, z, ext));
                }
            });
        }


        #endregion


        private readonly T[] _values;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="nx"></param>
        /// <param name="ny"></param>
        /// <param name="nz"></param>
        protected GridField3d(Domain3d domain, int nx, int ny, int nz)
          : base(domain, nx, ny, nz)
        {
            _values = new T[Count];
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="nx"></param>
        /// <param name="ny"></param>
        /// <param name="nz"></param>
        /// <param name="sampleMode"></param>
        /// <param name="wrapMode"></param>
        protected GridField3d(Domain3d domain, int nx, int ny, int nz, SampleMode sampleMode, WrapMode wrapMode)
            : base(domain, nx, ny, nz, sampleMode, wrapMode)
        {
            _values = new T[Count];
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="nx"></param>
        /// <param name="ny"></param>
        /// <param name="nz"></param>
        /// <param name="sampleMode"></param>
        /// <param name="wrapModeX"></param>
        /// <param name="wrapModeY"></param>
        /// <param name="wrapModeZ"></param>
        protected GridField3d(Domain3d domain, int nx, int ny, int nz, SampleMode sampleMode, WrapMode wrapModeX, WrapMode wrapModeY, WrapMode wrapModeZ)
            : base(domain, nx, ny, nz, sampleMode, wrapModeX, wrapModeY, wrapModeZ)
        {
            _values = new T[Count];
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        protected GridField3d(Grid3d other)
            : base(other)
        {
            _values = new T[Count];
        }


        /// <summary>
        /// 
        /// </summary>
        public T[] Values
        {
            get { return _values; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index]
        {
            get { return _values[index]; }
            set { _values[index] = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return _values[i];
        }


        /// <summary>
        /// Returns a deep copy of this field.
        /// </summary>
        /// <returns></returns>
        protected abstract GridField3d<T> DuplicateBase();


        /// <summary>
        /// Returns the value at the given indices.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public T ValueAt(int i, int j, int k)
        {
            return _values[IndexAt(i, j, k)];
        }


        /// <summary>
        /// Returns the value at the given indices.
        /// Assumes the indices are within the valid range.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public T ValueAtUnchecked(int i, int j, int k)
        {
            return _values[IndexAtUnchecked(i, j, k)];
        }


        /// <inheritdoc/>
        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public T ValueAt(Vec3d point)
        {
            switch (SampleMode)
            {
                case SampleMode.Nearest:
                    return _values[IndexAt(point)];
                case SampleMode.Linear:
                    return ValueAtLinear(point);
                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        /// Returns the value at the given point.
        /// Assumes the point is inside the field's domain.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public T ValueAtUnchecked(Vec3d point)
        {
            switch (SampleMode)
            {
                case SampleMode.Nearest:
                    return _values[IndexAtUnchecked(point)];
                case SampleMode.Linear:
                    return ValueAtLinearUnchecked(point);
                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        protected abstract T ValueAtLinear(Vec3d point);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        protected abstract T ValueAtLinearUnchecked(Vec3d point);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public abstract T ValueAt(GridPoint3d point);


        /// <summary>
        /// Sets all values along the boundary of the field to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundary(T value)
        {
            SetBoundaryXY(value);
            SetBoundaryXZ(value);
            SetBoundaryYZ(value);
        }


        /// <summary>
        /// Sets all values along the XY boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryXY(T value)
        {
            int offset = Count - CountXY;

            for (int i = 0; i < CountXY; i++)
                _values[i] = _values[i + offset] = value;
        }


        /// <summary>
        /// Sets all values along the XZ boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryXZ(T value)
        {
            int offset = CountXY - CountX;

            for (int k = 0; k < CountZ; k++)
            {
                int i0 = k * CountXY;
                int i1 = i0 + CountX;

                for (int i = i0; i < i1; i++)
                    _values[i] = _values[i + offset] = value;
            }
        }


        /// <summary>
        /// Sets all values along the YZ boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryYZ(T value)
        {
            int offset = CountX - 1;

            for (int i = 0; i < Count; i += CountX)
                _values[i] = _values[i + offset] = value;
        }


        /// <summary>
        /// Sets all values along the lower XY boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryXY0(T value)
        {
            for (int i = 0; i < CountXY; i++)
                _values[i] = value;
        }


        /// <summary>
        /// Sets all values along the upper XY boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryXY1(T value)
        {
            for (int i = Count - CountXY; i < Count; i++)
                _values[i] = value;
        }


        /// <summary>
        /// Sets all values along the lower YZ boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryXZ0(T value)
        {
            for (int k = 0; k < CountZ; k++)
            {
                int i0 = k * CountXY;
                int i1 = i0 + CountX;

                for (int i = i0; i < i1; i++)
                    _values[i] = value;
            }
        }


        /// <summary>
        /// Sets all values along the upper XZ boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryXZ1(T value)
        {
            int offset = CountXY - CountX;

            for (int k = 0; k < CountZ; k++)
            {
                int i0 = (k + 1) * CountXY - CountX;
                int i1 = i0 + CountX;

                for (int i = i0; i < i1; i++)
                    _values[i + offset] = value;
            }
        }


        /// <summary>
        /// Sets all values along the lower YZ boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryYZ0(T value)
        {
            for (int i = 0; i < Count; i += CountX)
                _values[i] = value;
        }


        /// <summary>
        /// Sets all values along the upper YZ boundary to a given constant.
        /// </summary>
        /// <param name="value"></param>
        public void SetBoundaryYZ1(T value)
        {
            for (int i = CountX - 1; i < Count; i += CountX)
                _values[i] = value;
        }


        /// <summary>
        /// Sets this field to some function of its coordinates.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parallel"></param>
        public void SpatialFunctionXYZ(Func<Vec3d, T> func, bool parallel = false)
        {
            Action<Tuple<int, int>> func2 = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }
                    _values[index] = func(CoordinateAt(i, j, k));
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func2);
            else
                func2(Tuple.Create(0, Count));
        }


        /// <summary>
        /// Sets this field to some function of its coordinates.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parallel"></param>
        public void SpatialFunctionXYZ(Func<double, double, double, T> func, bool parallel = false)
        {
            double x0 = Domain.X.T0;
            double y0 = Domain.Y.T0;
            double z0 = Domain.Z.T0;

            Action<Tuple<int, int>> func2 = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }
                    _values[index] = func(i * ScaleX + x0, j * ScaleY + y0, k * ScaleZ + z0);
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func2);
            else
                func2(Tuple.Create(0, Count));
        }


        /// <summary>
        /// Sets this field to some function of its normalized coordinates.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parallel"></param>
        public void SpatialFunctionUVW(Func<Vec3d, T> func, bool parallel = false)
        {
            double ti = 1.0 / (CountX - 1);
            double tj = 1.0 / (CountY - 1);
            double tk = 1.0 / (CountZ - 1);

            Action<Tuple<int, int>> func2 = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }

                    _values[index] = func(new Vec3d(i * ti, j * tj, k * tk));
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func2);
            else
                func2(Tuple.Create(0, Count));
        }


        /// <summary>
        /// Sets this field to some function of its normalized coordinates.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parallel"></param>
        public void SpatialFunctionUVW(Func<double, double, double, T> func, bool parallel = false)
        {
            double ti = 1.0 / (CountX - 1);
            double tj = 1.0 / (CountY - 1);
            double tk = 1.0 / (CountZ - 1);

            Action<Tuple<int, int>> func2 = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }

                    _values[index] = func(i * ti, j * tj, k * tk);
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func2);
            else
                func2(Tuple.Create(0, Count));
        }


        /// <summary>
        /// Sets this field to some function of its indices.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parallel"></param>
        public void SpatialFunctionIJK(Func<int, int, int, T> func, bool parallel = false)
        {
            Action<Tuple<int, int>> func2 = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }
                    _values[index] = func(i, j, k);
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func2);
            else
                func2(Tuple.Create(0, Count));
        }


        /// <summary>
        /// Sets this field to the values of another.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="parallel"></param>
        public void Sample(GridField3d<T> other, bool parallel = false)
        {
            if (ResolutionEquals(other))
            {
                _values.Set(other._values);
                return;
            }

            Sample((IField3d<T>)other, parallel);
        }


        /// <summary>
        /// Sets this field to the values of another.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="other"></param>
        /// <param name="converter"></param>
        /// <param name="parallel"></param>
        public void Sample<U>(GridField3d<U> other, Func<U, T> converter, bool parallel = false)
            where U : struct
        {
            if (ResolutionEquals(other))
            {
                other._values.Convert(converter, _values);
                return;
            }

            Sample((IField3d<U>)other, converter, parallel);
        }


        /// <summary>
        /// Sets this field to the values of another.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="parallel"></param>
        public void Sample(IField3d<T> other, bool parallel = false)
        {
            Action<Tuple<int, int>> func = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }
                    _values[index] = other.ValueAt(CoordinateAt(i, j, k));
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func);
            else
                func(Tuple.Create(0, Count));
        }


        /// <summary>
        /// Sets this field to the values of another.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="other"></param>
        /// <param name="converter"></param>
        /// <param name="parallel"></param>
        public void Sample<U>(IField3d<U> other, Func<U, T> converter, bool parallel = false)
        {
            Action<Tuple<int, int>> func = range =>
            {
                (int i, int j, int k) = IndicesAt(range.Item1);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == CountX) { j++; i = 0; }
                    if (j == CountY) { k++; j = 0; }
                    _values[index] = converter(other.ValueAt(CoordinateAt(i, j, k)));
                }
            };

            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), func);
            else
                func(Tuple.Create(0, Count));
        }


        #region Explicit interface implementations


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        T IField2d<T>.ValueAt(Vec2d point)
        {
            return ValueAt(new Vec3d(point.X, point.Y, 0.0));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IDiscreteField<T> IDiscreteField<T>.Duplicate()
        {
            return DuplicateBase();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        int IList<T>.IndexOf(T item)
        {
            for (int i = 0; i < Count; i++)
                if (item.Equals(_values[i])) return i;

            return -1;
        }


        /// <summary>
        /// 
        /// </summary>
        bool ICollection<T>.IsReadOnly
        {
            get { return false; }
        }


        /// <summary>
        /// 
        /// </summary>
        bool ICollection<T>.Contains(T item)
        {
            for (int i = 0; i < Count; i++)
                if (item.Equals(_values[i])) return true;

            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// 
        /// </summary>
        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// 
        /// </summary>
        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        void ICollection<T>.CopyTo(T[] array, int index)
        {
            array.SetRange(_values, index, 0, Count);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        #endregion
    }
}
