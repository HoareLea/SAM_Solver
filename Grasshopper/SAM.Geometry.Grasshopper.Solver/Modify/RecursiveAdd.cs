using System.Collections.Generic;

namespace SAM.Geometry.Grasshopper.Solver
{
    public static partial class Modify
    {
        public static void RecursiveAdd(int Current, ref List<int> Bucket, ref List<int>[] Pairs, ref bool[] Visited)
        {
            List<int> nextlist = new List<int>();

            foreach (int pair in Pairs[Current])
                if (!Visited[pair])
                {
                    Bucket.Add(pair);
                    nextlist.Add(pair);
                    Visited[pair] = true;
                }

            for (int i = 0; i < nextlist.Count; i++)
                RecursiveAdd(nextlist[i], ref Bucket, ref Pairs, ref Visited);
        }
    }
}
