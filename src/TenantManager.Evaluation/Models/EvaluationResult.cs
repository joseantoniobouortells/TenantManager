using System;

namespace TenantManager.Evaluation.Models
{
    /// <summary>
    /// Represents the result of evaluating a single LLM model.
    /// </summary>
    public class EvaluationResult
    {
        /// <summary>Identifier of the model (e.g., file name or model name)</summary>
        public string ModelId { get; set; } = string.Empty;
        /// <summary>Number of passed scenarios</summary>
        public int Passed { get; set; }
        /// <summary>Number of failed scenarios</summary>
        public int Failed { get; set; }
        /// <summary>Total scenarios evaluated</summary>
        public int Total => Passed + Failed;
        /// <summary>Elapsed time in milliseconds for the whole evaluation of this model</summary>
        public long ExecutionTimeMs { get; set; }
        /// <summary>Convenient percentage of success</summary>
        public double SuccessRate => Total == 0 ? 0.0 : (double)Passed / Total * 100.0;
    }
}
