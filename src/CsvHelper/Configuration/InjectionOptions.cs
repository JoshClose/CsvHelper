using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Options for handling injection attacks.
	/// </summary>
	public enum InjectionOptions
	{
		/// <summary>
		/// No injection protection.
		/// </summary>
		None = 0,
		/// <summary>
		/// Escape injection characters.
		/// </summary>
		Escape,
		/// <summary>
		/// Strip injection characters.
		/// </summary>
		Strip,
		/// <summary>
		/// Throw an exception if injection characters are detected.
		/// </summary>
		Exception,
	}
}
