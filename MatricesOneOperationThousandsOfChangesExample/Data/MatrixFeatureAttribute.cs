using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// Marks a property as a matrix-mapped feature.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MatrixFeatureAttribute : Attribute
    {
    }
}
