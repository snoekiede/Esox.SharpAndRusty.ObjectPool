using System;
using System.Collections.Generic;
using System.Text;
using Esox.SharpAndRusty.ObjectPool.CircuitBreaker;


namespace Esox.SharpAndRusty.ObjectPool.Exceptions
{
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Circuit breaker statistics at the time of exception
        /// </summary>
        public CircuitBreakerStatistics Statistics { get; }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class using the specified circuit breaker
        /// statistics.
        /// </summary>
        /// <remarks>This exception is typically thrown when an operation is attempted while the circuit breaker
        /// is in the open state, indicating that further calls are temporarily blocked due to recent failures. The provided
        /// statistics can be used to inspect the failure history and the time when the circuit was opened.</remarks>
        /// <param name="statistics">The statistics associated with the circuit breaker at the time it was opened. Cannot be null.</param>
        public CircuitBreakerOpenException(CircuitBreakerStatistics statistics)
            : base($"Circuit breaker is open. Consecutive failures: {statistics.ConsecutiveFailures}, " +
                   $"Failure rate: {statistics.FailurePercentage:F2}%. Circuit opened at: {statistics.CircuitOpenedAt}")
        {
            Statistics = statistics;
        }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class with a specified error message and circuit
        /// breaker statistics.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="statistics">The statistics associated with the circuit breaker at the time the exception was thrown. Cannot be null.</param>
        public CircuitBreakerOpenException(string message, CircuitBreakerStatistics statistics)
            : base(message)
        {
            Statistics = statistics;
        }
    }

}
