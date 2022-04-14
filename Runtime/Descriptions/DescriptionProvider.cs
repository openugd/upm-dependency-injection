using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenUGD.Descriptions
{
    public class DescriptionProvider
    {
        private static readonly HashSet<TypeProvider> EmptyTypeProviders = new HashSet<TypeProvider>();

        private static readonly Type AttributeType = typeof(Attribute);

        private readonly HashSet<Type> _mappedAttributes = new HashSet<Type>();
        private readonly Dictionary<Type, TypeProvider> _byType = new Dictionary<Type, TypeProvider>();

        private readonly Dictionary<Type, HashSet<TypeProvider>> _byAttribute =
            new Dictionary<Type, HashSet<TypeProvider>>();

        private readonly DescriptionProvider _parent;

        public DescriptionProvider(DescriptionProvider parent = null)
        {
            _parent = parent;
        }

        public virtual DescriptionProvider Parent => _parent;

        public virtual void MapAttribute<T>() where T : Attribute
        {
            _mappedAttributes.Add(typeof(T));
        }

        public virtual void MapAttribute(Type type)
        {
            if (!type.IsSubclassOf(AttributeType)) throw new ArgumentException();
            _mappedAttributes.Add(type);
        }

        public virtual void UnMapAttribute<T>() where T : Attribute
        {
            _mappedAttributes.Remove(typeof(T));
        }

        public virtual void UnMapAttribute(Type type)
        {
            if (!type.IsSubclassOf(AttributeType)) throw new ArgumentException();
            _mappedAttributes.Remove(type);
        }

        public virtual bool IsMappedAttribute(Type type, bool inherited = true)
        {
            if (type == null) return false;
            return _mappedAttributes.Contains(type) ||
                   (inherited && _parent != null && _parent.IsMappedAttribute(type));
        }

        public virtual Types MappedAttributes(bool inherited = true)
        {
            if (inherited && _parent != null)
            {
                return new Types(_mappedAttributes, _parent);
            }

            return new Types(_mappedAttributes);
        }

        public virtual TypeProvider AddProvider(TypeProvider provider)
        {
            AddTypeProvider(provider);
            if (provider.Parsed)
            {
                AddToAttribute(provider);
            }

            return provider;
        }

        public virtual void ParseProvider(TypeProvider provider, MemberKind kind = MemberKind.All)
        {
            AddTypeProvider(provider);
            provider.Parse(kind);
            AddToAttribute(provider);
        }

        public virtual void AddProvider<T>(MemberKind kind = MemberKind.All)
        {
            AddProvider(typeof(T), kind);
        }

        public virtual void AddProvider(Type type, MemberKind kind = MemberKind.All)
        {
            if (GetTypeProvider(type, kind) == null)
            {
                CreateTypeProvider(type, kind);
            }
        }

        public virtual TypeProvider GetProvider<T>(MemberKind kind = MemberKind.All) where T : class
        {
            return GetProvider(typeof(T), kind);
        }

        public virtual TypeProvider GetProvider(Type type, MemberKind kind = MemberKind.All)
        {
            var result = GetTypeProvider(type, kind) ?? CreateTypeProvider(type, kind);
            return result;
        }

        public virtual TypeProviders GetProvidersByAttribute<T>() where T : Attribute
        {
            HashSet<TypeProvider> result;
            _byAttribute.TryGetValue(typeof(T), out result);
            return new TypeProviders(result);
        }

        protected virtual void AddTypeProvider(TypeProvider provider)
        {
            _byType.Add(provider.Type, provider);
        }

        protected virtual TypeProvider GetTypeProvider(Type type, MemberKind kind = MemberKind.All)
        {
            TypeProvider result;
            if (!_byType.TryGetValue(type, out result) && _parent != null)
            {
                return _parent.GetTypeProvider(type, kind);
            }

            if (result != null && !result.Parsed)
            {
                result.Parse(kind);
                AddToAttribute(result);
            }

            return result;
        }

        protected virtual TypeProvider CreateTypeProvider(Type type, MemberKind kind = MemberKind.All)
        {
            var provider = new TypeProvider(this, type);
            AddTypeProvider(provider);
            provider.Parse(kind);
            AddToAttribute(provider);
            return provider;
        }

        private void AddToAttribute(TypeProvider provider)
        {
            foreach (var attribute in provider.MembersAttributes)
            {
                HashSet<TypeProvider> hashSet;
                if (!_byAttribute.TryGetValue(attribute, out hashSet))
                {
                    hashSet = new HashSet<TypeProvider>();
                    _byAttribute.Add(attribute, hashSet);
                }

                hashSet.Add(provider);
            }

            foreach (var attribute in provider.TypeAttributes)
            {
                Type attributeType = attribute.GetType();
                HashSet<TypeProvider> hashSet;
                if (!_byAttribute.TryGetValue(attributeType, out hashSet))
                {
                    hashSet = new HashSet<TypeProvider>();
                    _byAttribute.Add(attributeType, hashSet);
                }

                hashSet.Add(provider);
            }
        }

        public struct TypeProviders : IEnumerable<TypeProvider>
        {
            private readonly HashSet<TypeProvider> _providers;

            public TypeProviders(HashSet<TypeProvider> providers)
            {
                if (providers == null)
                {
                    providers = EmptyTypeProviders;
                }
                _providers = providers;
            }

            public HashSet<TypeProvider>.Enumerator GetEnumerator()
            {
                return _providers.GetEnumerator();
            }

            IEnumerator<TypeProvider> IEnumerable<TypeProvider>.GetEnumerator()
            {
                return _providers.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _providers.GetEnumerator();
            }
        }

        public struct Types : IEnumerable<Type>
        {
            private HashSet<Type> _hashSet;
            private DescriptionProvider _parent;

            public Types(HashSet<Type> hashSet)
            {
                _hashSet = hashSet;
                _parent = default;
            }

            public Types(HashSet<Type> hashSet, DescriptionProvider parent)
            {
                _hashSet = hashSet;
                _parent = parent;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            IEnumerator<Type> IEnumerable<Type>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            [Serializable]
            public struct Enumerator : IEnumerator<Type>
            {
                private readonly Types _types;
                private Types _current;
                private HashSet<Type>.Enumerator _enumerator;

                public Enumerator(Types types)
                {
                    _types = types;

                    _current = _types;
                    _enumerator = _current._hashSet.GetEnumerator();
                }

                public bool MoveNext()
                {
                    var result = _enumerator.MoveNext();
                    if (!result)
                    {
                        while (_current._parent != null)
                        {
                            _current = _current._parent.MappedAttributes(true);
                            _enumerator = _current._hashSet.GetEnumerator();
                            result = _enumerator.MoveNext();
                            if (result)
                            {
                                break;
                            }
                        }
                    }
                    return result;
                }

                public void Reset()
                {
                    _current = _types;
                    _enumerator = _current._hashSet.GetEnumerator();
                }

                public Type Current => _enumerator.Current;

                object IEnumerator.Current => _enumerator.Current;

                public void Dispose()
                {

                }
            }
        }
    }
}