using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Solver
{
    public static partial class Query
    {
        public static List<List<int>> FindBuckets(this List<Line> lines, double bucketSize)
        {
            List<Line> extendedLines = new List<Line>(lines);

            //extend lines so they're a bit longer than bucketsize
            for (int i = 0; i < lines.Count; i++)
            {
                Line item = extendedLines[i];
                item.Extend(bucketSize * 2, bucketSize * 2);
                extendedLines[i] = item;
            }

            //find pairs of lines where score < bucketsize
            List<int>[] pairs = new List<int>[lines.Count];

            for (int i = 0; i < lines.Count; i++)
                pairs[i] = new List<int>();

            for (int i = 0; i < lines.Count; i++)
                for (int j = 0; j < lines.Count; j++)
                {
                    if (i == j) continue;
                    if (j > i) break;

                    double thisscore = Similarity(extendedLines[i], extendedLines[j]);
                    if (thisscore <= bucketSize)
                    {
                        pairs[i].Add(j);
                        pairs[j].Add(i);
                    }
                }

            //buckets
            List<List<int>> buckets = new List<List<int>>();
            bool[] visited = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                List<int> thisbucket = new List<int>();
                //find not visited
                int start = -1;
                for (int j = 0; j < visited.Length; j++)
                    if (!visited[j])
                    {
                        start = j;
                        visited[j] = true;
                        break;
                    }
                if (start == -1) break;

                thisbucket.Add(start);
                Modify.RecursiveAdd(start, ref thisbucket, ref pairs, ref visited);
                if (thisbucket.Count > 0) buckets.Add(thisbucket);
            }

            //sort buckets by length
            for (int i = 0; i < buckets.Count; i++)
            {
                List<int> thisbucket = buckets[i];
                double[] len = new double[thisbucket.Count];
                int[] ids = new int[thisbucket.Count];

                for (int j = 0; j < thisbucket.Count; j++)
                {
                    len[j] = lines[thisbucket[j]].Length;
                    ids[j] = thisbucket[j];
                }

                Array.Sort(len, ids);
                Array.Reverse(ids);

                thisbucket.Clear();
                thisbucket.AddRange(ids);
            }

            List<List<int>> fixedBuckets = new List<List<int>>();

            for (int i = 0; i < buckets.Count; i++)
                fixedBuckets.AddRange(SplitBucket(buckets[i], bucketSize, lines));

            return fixedBuckets;
        }
    }
}
