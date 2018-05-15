using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MHWSharpnessExtractor
{
    public interface IDataSource
    {
        string Name { get; }
        Task<IList<Weapon>> ProduceWeaponsAsync();
    }
}
