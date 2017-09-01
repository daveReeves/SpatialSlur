﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 * Notes
 */ 

namespace SpatialSlur.SlurCore
{
    /// <summary>
    /// 
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="random"></param>
        /// <returns></returns>
        public static Domain1d NextDomain(this Random random)
        {
            return new Domain1d(random.NextDouble(), random.NextDouble());
        }


        /// <summary>
        /// returns a random 2d vector with a 0.0 to 1.0 range in each dimension
        /// </summary>
        /// <param name="random"></param>
        public static Vec2d NextVec2d(this Random random)
        {
            return new Vec2d(random.NextDouble(), random.NextDouble());
        }


        /// <summary>
        /// Returns a random vector which has components within the given domain.
        /// </summary>
        /// <param name="random"></param>
        /// <param name="t0"></param>
        /// <param name="t1"></param>
        /// <returns></returns>
        public static Vec2d NextVec2d(this Random random, double t0, double t1)
        {
            return new Vec2d(
                SlurMath.Lerp(t0, t1, random.NextDouble()),
                SlurMath.Lerp(t0, t1, random.NextDouble()));
        }


        /// <summary>
        /// Returns a random 2d vector which has components within the given domain
        /// </summary>
        /// <param name="random"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static Vec2d NextVec2d(this Random random, Domain1d domain)
        {
            return new Vec2d(
                domain.Evaluate(random.NextDouble()),
                domain.Evaluate(random.NextDouble()));
        }


        /// <summary>
        /// Returns a random 2d vector which has components within the given domain
        /// </summary>
        /// <param name="random"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static Vec2d NextVec2d(this Random random, Domain2d domain)
        {
            return new Vec2d(
                domain.X.Evaluate(random.NextDouble()),
                domain.Y.Evaluate(random.NextDouble()));
        }


        /// <summary>
        /// returns a random vector with a 0.0 to 1.0 range in each dimension.
        /// </summary>
        /// <param name="random"></param>
        public static Vec3d NextVec3d(this Random random)
        {
            return new Vec3d(random.NextDouble(), random.NextDouble(), random.NextDouble());
        }


        /// <summary>
        /// Returns a random vector which has components within the given domain.
        /// </summary>
        /// <param name="random"></param>
        /// <param name="t0"></param>
        /// <param name="t1"></param>
        /// <returns></returns>
        public static Vec2d NextVec3d(this Random random, double t0, double t1)
        {
            return new Vec3d(
                SlurMath.Lerp(t0, t1, random.NextDouble()),
                SlurMath.Lerp(t0, t1, random.NextDouble()),
                SlurMath.Lerp(t0, t1, random.NextDouble()));
        }


        /// <summary>
        /// Returns a random vector which has components within the given domain.
        /// </summary>
        /// <param name="random"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static Vec3d NextVec3d(this Random random, Domain1d domain)
        {
            return new Vec3d(
                domain.Evaluate(random.NextDouble()),
                domain.Evaluate(random.NextDouble()),
                domain.Evaluate(random.NextDouble()));
        }


        /// <summary>
        /// Returns a random vector which has components within the given domain.
        /// </summary>
        /// <param name="random"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static Vec3d NextVec3d(this Random random, Domain3d domain)
        {
            return new Vec3d(
                domain.X.Evaluate(random.NextDouble()),
                domain.Y.Evaluate(random.NextDouble()),
                domain.Z.Evaluate(random.NextDouble()));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="random"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public static T NextItem<T>(this Random random, IReadOnlyList<T> items)
        {
            return items[random.Next(items.Count)];
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="random"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public static T NextItem<T>(this Random random, T[] items)
        {
            return items[random.Next(items.Length)];
        }
    }
}
