using System;

namespace SAM.Core.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="bottom"></param>
        /// <param name="top"></param>
        /// <returns></returns>
        public static double Clamp(this double parameter, double bottom, double top)
        {
            if (parameter < bottom) return bottom;
            if (parameter > top) return top;
            return parameter;
        }
    }
}