﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialSlur.SlurCore;

namespace SpatialSlur.SlurData
{
    /// <summary>
    /// http://www.beosil.com/download/CollisionDetectionHashing_VMV03.pdf
    /// http://www.slaney.org/malcolm/yahoo/Slaney2008-LSHTutorial.pdf
    /// </summary>
    public class SpatialHash3d<T>:Spatial3d<T>
    {
        // prime numbers used in hash function
        private const int P1 = 73856093;
        private const int P2 = 19349663;
        private const int P3 = 83492791;

        private double _scale, _scaleInv; // scale of implicit grid

      
        /// <summary>
        /// 
        /// </summary>
        public SpatialHash3d(int binCount, double scale)
            :base(binCount)
        {
            BinScale = scale;
        }


        /// <summary>
        /// Gets or sets the scale of the implicit grid.
        /// Note that setting the scale clears the map.
        /// </summary>
        public double BinScale
        {
            get { return _scale; }
            set
            {
                if (value <= 0.0)
                    throw new ArgumentOutOfRangeException("The scale must be larger than zero");

                _scale = value;
                _scaleInv = 1.0 / _scale;
                Clear();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        protected override void Discretize(Vec3d point, out int i, out int j, out int k)
        {
            i = (int)Math.Floor(point.x * _scaleInv);
            j = (int)Math.Floor(point.y * _scaleInv);
            k = (int)Math.Floor(point.z * _scaleInv);
        }


        /// <summary>
        /// TODO test different hash functions
        /// http://cybertron.cg.tu-berlin.de/eitz/pdf/2007_hsh.pdf
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        protected override int ToIndex(int i, int j, int k)
        {
            return SlurMath.Mod2(i * P1 ^ j * P2 ^ k * P3, BinCount);
        }

    }
}
