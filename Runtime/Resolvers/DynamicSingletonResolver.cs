using System;

namespace OpenUGD.Resolvers
{
    public class DynamicSingletonResolver : IResolver, IResolverHook
    {
        private object _instance;
        private Func<object> _valueProvider;
        private Func<Type, object> _valueProviderDynamic;

        public DynamicSingletonResolver(Func<object> valueProvider)
        {
            _valueProvider = valueProvider;
        }

        public DynamicSingletonResolver(Func<Type, object> valueProvider)
        {
            _valueProviderDynamic = valueProvider;
        }

        public object Resolve(IInjector injector, Type type)
        {
            if (_instance == null)
            {
                if (_valueProvider != null)
                {
                    _instance = _valueProvider();
                }
                else
                {
                    _instance = _valueProviderDynamic(type);
                }
                injector.Inject(_instance);
            }

            return _instance;
        }

        public void OnRegister(IInjector injector)
        {
        }

        public void OnUnRegister()
        {
            _valueProvider = null;
            _valueProviderDynamic = null;
        }
    }

    public class DynamicSingletonResolver<T> : IResolver, IResolverHook
    {
        private object _instance;
        private Func<T> _valueProvider;

        public DynamicSingletonResolver(Func<T> valueProvider)
        {
            _valueProvider = valueProvider;
        }

        public object Resolve(IInjector injector, Type type)
        {
            if (_instance == null)
            {
                _instance = _valueProvider();
                injector.Inject(_instance);
            }

            return _instance;
        }

        public void OnRegister(IInjector injector)
        {
        }

        public void OnUnRegister()
        {
            _valueProvider = null;
        }
    }
}
