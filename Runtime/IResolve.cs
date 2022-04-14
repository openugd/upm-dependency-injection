using System;

namespace OpenUGD
{
    public interface IResolve
    {
        object Resolve(Type type);
    }
}