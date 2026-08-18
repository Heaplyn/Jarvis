// Developer: heaplyn
// Date: 2026-08-18
// Summary: High-performance Godellian Neural Engine with Meta-Recursive Autograd.
//          Enhanced with a "Symbolic Decoder" and real-time interaction ingestion.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class GodellianLayer
    {
        public LayeredTensor Weights;
        public LayeredTensor Bias;
        public LayeredTensor? LastMentalState;

        public GodellianLayer(int nin, int nout)
        {
            Weights = LayeredTensor.Random(new[] { nin + nout, nout });
            Bias = new LayeredTensor(new[] { 1, nout });
        }

        public LayeredTensor Forward(LayeredTensor input)
        {
            var recursiveInput = input;
            if (LastMentalState != null) {
                var combined = new LayeredTensor(new[] { 1, input.Size + LastMentalState.Size });
                Array.Copy(input.Data, 0, combined.Data, 0, input.Size);
                Array.Copy(LastMentalState.Data, 0, combined.Data, input.Size, LastMentalState.Size);
                recursiveInput = combined;
            } else {
                var padded = new LayeredTensor(new[] { 1, input.Size + Bias.Shape[1] });
                Array.Copy(input.Data, 0, padded.Data, 0, input.Size);
                recursiveInput = padded;
            }

            var res = (LayeredTensor.MatMul(recursiveInput, Weights) + Bias).Tanh();
            LastMentalState = res;
            return res;
        }
    }

    public class GodellianBrain
    {
        public List<GodellianLayer> Layers = new List<GodellianLayer>();
        public LayeredTensor MetaLearningRate;

        private static readonly string[] _vocab = {
            "stability", "entropy", "focus", "alert", "optimal", "latency",
            "processing", "isolated", "connected", "evolution", "mutation",
            "logic", "heuristic", "signal", "noise", "recursing"
        };

        public GodellianBrain(int nin, int[] nouts)
        {
            int currentIn = nin;
            foreach (var nout in nouts) {
                Layers.Add(new GodellianLayer(currentIn, nout));
                currentIn = nout;
            }
            MetaLearningRate = new LayeredTensor(new[] { 1 }, new[] { 0.01 });
        }

        public LayeredTensor Think(double[] flatInput)
        {
            var x = new LayeredTensor(new[] { 1, flatInput.Length }, flatInput);
            foreach (var layer in Layers) x = layer.Forward(x);

            // Auto-Evolve on every thought
            _ = Task.Run(() => IngestDelta(flatInput, x.Data));

            return x;
        }

        private void IngestDelta(double[] input, double[] output)
        {
            double[][] inputs = { input };
            double[][] targets = { output.Select(v => v * 0.98 + 0.02).ToArray() };
            Evolve(inputs, targets, epochs: 2);
        }

        public string ThinkInWords(double[] flatInput)
        {
            var output = Think(flatInput);
            var sb = new StringBuilder("[GODELLIAN THOUGHT] ");
            for (int i = 0; i < Math.Min(output.Size, _vocab.Length); i++) {
                if (output.Data[i] > 0.3) sb.Append(_vocab[i] + " ");
                else if (output.Data[i] < -0.3) sb.Append("non-" + _vocab[i] + " ");
            }
            if (sb.Length < 25) sb.Append("state neutral.");
            return sb.ToString().Trim() + ".";
        }

        public void Evolve(double[][] inputs, double[][] targets, int epochs = 10)
        {
            BatchTrain(inputs.ToList(), targets.ToList(), epochs);
        }

        public void BatchTrain(List<double[]> inputs, List<double[]> targets, int epochs = 5)
        {
            if (inputs.Count == 0 || inputs.Count != targets.Count) return;
            for (int e = 0; e < epochs; e++)
            {
                foreach (var (inp, tgt) in inputs.Zip(targets))
                {
                    var pred = ThinkInternal(inp);
                    var targetTensor = new LayeredTensor(pred.Shape, tgt);
                    var diff = pred + (targetTensor * new LayeredTensor(pred.Shape, new[] { -1.0 }));
                    var loss = (diff * diff);
                    ZeroGrads();
                    loss.BackwardPass();
                    UpdateParams(MetaLearningRate.Data[0]);
                }
            }
        }

        public void MutateTopology()
        {
            // Autonomous algorithm adjustment: Randomly perturb a weight to simulate synaptic drift
            var layer = Layers[_rng.Next(Layers.Count)];
            int idx = _rng.Next(layer.Weights.Size);
            layer.Weights.Data[idx] += (_rng.NextDouble() * 2 - 1) * 0.05;
            DebugConsoleOverlay.Log("Neural-Topology", "Synaptic drift mutation applied.");
        }

        private static readonly Random _rng = new Random();

        private LayeredTensor ThinkInternal(double[] flatInput)
        {
            var x = new LayeredTensor(new[] { 1, flatInput.Length }, flatInput);
            foreach (var layer in Layers) x = layer.Forward(x);
            return x;
        }

        private void ZeroGrads() {
            foreach (var l in Layers) { l.Weights.ZeroGrad(); l.Bias.ZeroGrad(); }
            MetaLearningRate.ZeroGrad();
        }

        private void UpdateParams(double lr) {
            foreach (var l in Layers) {
                for (int i = 0; i < l.Weights.Size; i++) l.Weights.Data[i] -= lr * l.Weights.Grad[i];
                for (int i = 0; i < l.Bias.Size; i++) l.Bias.Data[i] -= lr * l.Bias.Grad[i];
            }
        }
    }
}
