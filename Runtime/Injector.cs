using System;
using System.Collections;
using System.Collections.Generic;
using OpenUGD.Descriptions;

namespace OpenUGD
{
    public class Injector<T> : Injector
    {
        public Injector() : base(typeof(T))
        {
        }
    }

    public class Injector : IInjector, IEnumerable<KeyValuePair<Type, IResolver>>
    {
        private readonly Dictionary<Type, IResolver> _resolvers;
        private readonly Type _injectAttribute;

        public DescriptionProvider DescriptionProvider { get; }
        public IInjector Parent { get; }
        public Type InjectAttribute => _injectAttribute;

        public Injector() : this((IInjector)null, typeof(InjectAttribute))
        {
        }

        public Injector(Type injectAttribute) : this((IInjector)null, injectAttribute)
        {

        }

        public Injector(IInjector parent) : this(parent, null)
        {

        }

        private Injector(IInjector parent, Type injectAttribute = null)
        {
            if (parent != null)
            {
                _injectAttribute = parent.InjectAttribute;
                Parent = parent;
                DescriptionProvider = parent.DescriptionProvider;
            }
            else
            {
                if (injectAttribute == null) throw new ArgumentNullException(nameof(injectAttribute));
                if (!injectAttribute.IsSubclassOf(typeof(Attribute)))
                {
                    throw new ArgumentException($"{nameof(injectAttribute)} must be subclass of {nameof(Attribute)}");
                }
                _injectAttribute = injectAttribute;

                DescriptionProvider = new DescriptionProvider();
                DescriptionProvider.MapAttribute(_injectAttribute);
            }

            _resolvers = new Dictionary<Type, IResolver>();

            this.ToValue<IInjector>(this);
            this.ToValue<IInject>(this);
            this.ToValue<IResolve>(this);
            this.ToValue(this);
        }

        public void Register(Type type, IResolver resolver)
        {
            var current = GetResolver(type, false);
            if (current != resolver)
            {
                UnRegister(type);
                _resolvers[type] = resolver ?? throw new NullReferenceException();
                var hook = resolver as IResolverHook;
                hook?.OnRegister(this);
            }
        }

        public void UnRegister(Type type)
        {
            IResolver resolver;
            if (_resolvers.TryGetValue(type, out resolver))
            {
                var hook = resolver as IResolverHook;
                hook?.OnUnRegister();
                _resolvers.Remove(type);
            }
        }

        public IResolver GetResolver(Type type, bool includeInParents)
        {
            IResolver resolver;
            if (!_resolvers.TryGetValue(type, out resolver) && includeInParents && Parent != null)
            {
                resolver = Parent.GetResolver(type, true);
            }

            return resolver;
        }

        public void Inject(object value)
        {
            if (value != null)
            {
                var typeProvider = DescriptionProvider.GetProvider(value.GetType());
                ApplyResolver(value, typeProvider);
            }
        }

        public object Resolve(Type type)
        {
            var resolver = GetResolver(type, true);
            if (resolver != null)
            {
                return resolver.Resolve(this, type);
            }

            return null;
        }

        private void ApplyResolver(object value, TypeProvider typeProvider)
        {
            if (typeProvider.Parent != null)
            {
                ApplyResolver(value, typeProvider.Parent);
            }

            var members = typeProvider.GetByAttribute(InjectAttribute);
            if (members.Count != 0)
            {
                foreach (var member in members)
                {
                    var kind = member.Kind;
                    if ((kind & MemberKind.Field) == MemberKind.Field ||
                        (kind & MemberKind.Property) == MemberKind.Property)
                    {
                        var provider = GetResolver(member.ProviderType, true);
                        if (provider != null)
                        {
                            if (member.ProviderType != member.Type)
                            {
                                member.SetValue(value, CreateLazy(provider, member.Type, member.ProviderType));
                            }
                            else
                            {
                                member.SetValue(value, provider.Resolve(this, member.Type));
                            }
                        }
                    }
                }
            }
        }

        private object CreateLazy(IResolver provider, Type type, Type providerType)
        {
            Func<object> factory = () => provider.Resolve(this, type);
            return Activator.CreateInstance(typeof(Lazy<>).MakeGenericType(providerType), factory);
        }

        public Dictionary<Type, IResolver>.Enumerator GetEnumerator()
        {
            return _resolvers.GetEnumerator();
        }

        IEnumerator<KeyValuePair<Type, IResolver>> IEnumerable<KeyValuePair<Type, IResolver>>.GetEnumerator()
        {
            return _resolvers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _resolvers.GetEnumerator();
        }
    }
}