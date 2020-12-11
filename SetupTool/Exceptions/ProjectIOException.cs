using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Exceptions
{
	public class ProjectIOException : Exception
	{
		public ProjectIOException() { }

		public ProjectIOException(string message) : base(message) { }

		public ProjectIOException(string message, Exception innerException) : base(message, innerException) { }

		protected ProjectIOException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
