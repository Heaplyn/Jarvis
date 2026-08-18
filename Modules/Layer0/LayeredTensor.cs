// Developer: heaplyn
// Date: 2026-08-18
// Summary: High-performance N-Dimensional Tensor with Autograd & Vectorized Ops.
//          Optimized for "Godellian" recursive layering and massive vectorization.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JarvisLauncher
{
    public class LayeredTensor
    {
        public double[] Data;
        public double[] Grad;
        public int[] Shape;
        public int[] Strides;
        public int Size;

        public Action? Backward;
        public HashSet<LayeredTensor> Prev;
        public string Op;

        public LayeredTensor(int[] shape, double[]? data = null)
        {
            Shape = (int[])shape.Clone();
            Size = shape.Aggregate(1, (a, b) => a * b);
            Data = data ?? new double[Size];
            Grad = new double[Size];
            Strides = CalculateStrides(shape);
            Prev = new HashSet<LayeredTensor>();
            Op = "";
        }

        private static int[] CalculateStrides(int[] shape)
        {
            int[] strides = new int[shape.Length];
            int stride = 1;
            for (int i = shape.Length - 1; i >= 0; i--)
            {
                strides[i] = stride;
                stride *= shape[i];
            }
            return strides;
        }

        // --- CORE MATH OPS (Vectorized) ---

        public static LayeredTensor operator +(LayeredTensor a, LayeredTensor b)
        {
            var res = new LayeredTensor(a.Shape);
            for (int i = 0; i < a.Size; i++) res.Data[i] = a.Data[i] + b.Data[i];
            res.Op = "+"; res.Prev.Add(a); res.Prev.Add(b);
            res.Backward = () => {
                for (int i = 0; i < a.Size; i++) { a.Grad[i] += res.Grad[i]; b.Grad[i] += res.Grad[i]; }
            };
            return res;
        }

        public static LayeredTensor operator *(LayeredTensor a, LayeredTensor b)
        {
            var res = new LayeredTensor(a.Shape);
            for (int i = 0; i < a.Size; i++) res.Data[i] = a.Data[i] * b.Data[i];
            res.Op = "*"; res.Prev.Add(a); res.Prev.Add(b);
            res.Backward = () => {
                for (int i = 0; i < a.Size; i++) { a.Grad[i] += b.Data[i] * res.Grad[i]; b.Grad[i] += a.Data[i] * res.Grad[i]; }
            };
            return res;
        }

        public static LayeredTensor MatMul(LayeredTensor a, LayeredTensor b)
        {
            // Assuming (M x K) @ (K x N)
            int M = a.Shape[0]; int K = a.Shape[1]; int N = b.Shape[1];
            var res = new LayeredTensor(new[] { M, N });

            for (int i = 0; i < M; i++)
                for (int k = 0; k < K; k++)
                    for (int j = 0; j < N; j++)
                        res.Data[i * N + j] += a.Data[i * K + k] * b.Data[k * N + j];

            res.Op = "matmul"; res.Prev.Add(a); res.Prev.Add(b);
            res.Backward = () => {
                for (int i = 0; i < M; i++)
                    for (int k = 0; k < K; k++)
                        for (int j = 0; j < N; j++) {
                            a.Grad[i * K + k] += b.Data[k * N + j] * res.Grad[i * N + j];
                            b.Grad[k * N + j] += a.Data[i * K + k] * res.Grad[i * N + j];
                        }
            };
            return res;
        }

        public LayeredTensor Tanh()
        {
            var res = new LayeredTensor(Shape);
            for (int i = 0; i < Size; i++) res.Data[i] = Math.Tanh(Data[i]);
            res.Op = "tanh"; res.Prev.Add(this);
            res.Backward = () => {
                for (int i = 0; i < Size; i++) { double t = res.Data[i]; Grad[i] += (1 - t * t) * res.Grad[i]; }
            };
            return res;
        }

        // --- GODELLIAN POLYNOMIAL EXTENSION ---
        public LayeredTensor Polynomial(Dictionary<int, double> terms)
        {
            var res = new LayeredTensor(Shape);
            for (int i = 0; i < Size; i++) {
                double val = 0;
                foreach (var term in terms) val += term.Value * Math.Pow(Data[i], term.Key);
                res.Data[i] = val;
            }
            res.Op = "poly"; res.Prev.Add(this);
            res.Backward = () => {
                for (int i = 0; i < Size; i++) {
                    double derivative = 0;
                    foreach (var term in terms) derivative += term.Value * term.Key * Math.Pow(Data[i], term.Key - 1);
                    Grad[i] += derivative * res.Grad[i];
                }
            };
            return res;
        }

        public void BackwardPass()
        {
            var topo = new List<LayeredTensor>();
            var visited = new HashSet<LayeredTensor>();
            void Build(LayeredTensor t) {
                if (!visited.Contains(t)) {
                    visited.Add(t);
                    foreach (var p in t.Prev) Build(p);
                    topo.Add(t);
                }
            }
            Build(this);
            Array.Fill(Grad, 1.0);
            topo.Reverse();
            foreach (var t in topo) t.Backward?.Invoke();
        }

        public void ZeroGrad() => Array.Fill(Grad, 0);

        public static LayeredTensor Random(int[] shape) {
            var r = new Random();
            var t = new LayeredTensor(shape);
            for (int i = 0; i < t.Size; i++) t.Data[i] = r.NextDouble() * 2 - 1;
            return t;
        }
    }
}
