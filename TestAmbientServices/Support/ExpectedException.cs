using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// An exception that indicates that a probable infinite loop was detected and aborted.
    /// </summary>
    [Serializable]
    class ExpectedException : Exception
    {
        private readonly string _testName;

        /// <summary>
        /// Gets the name of the test where this exception was expected.
        /// </summary>
        public string TestName { get { return _testName; } }

        /// <summary>
        /// Constructs an expected test exception.
        /// </summary>
        /// <param name="testName">The name of the test case this exception is expected to occur in.</param>
        public ExpectedException(string testName)
            : base("This exception is expected to occur in the " + testName + " test!")
        {
            _testName = testName;
        }
        /// <summary>
        /// Constructs an expected test exception.
        /// </summary>
        public ExpectedException()
            : base("This exception is expected to occur during testing!")
        {
            _testName = null;
        }
    }
}
