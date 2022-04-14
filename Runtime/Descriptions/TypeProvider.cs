using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenUGD.Descriptions
{
    public class TypeProvider : IEquatable<TypeProvider>
    {
        protected static readonly Type ObjectType = typeof(object);
        private static int _instanceCount;

        private readonly int _index;
        private readonly Type _type;
        private readonly Type _baseType;
        private readonly int _memberCapacity;
        private readonly DescriptionProvider _provider;
        private HashSet<Type> _membersAttributes;
        private List<Attribute> _typeAttributes;
        private Dictionary<Type, List<MemberDescription>> _members;
        private TypeProvider _parent;
        private bool _parsed;

        public TypeProvider(DescriptionProvider provider, Type type, Type baseType = null, int memberCapacity = 4)
        {
            _index = Interlocked.Increment(ref _instanceCount);
            _provider = provider;
            _type = type;
            _memberCapacity = memberCapacity;
            _baseType = baseType;
        }

        public virtual bool Parsed => _parsed;

        public virtual KindMemberDescriptions GetByAttribute<T>() where T : Attribute
        {
            return GetByAttribute(typeof(T));
        }

        public virtual KindMemberDescriptions GetByAttribute<T>(MemberKind kind, bool autoRelease = false)
            where T : Attribute
        {
            return GetByAttribute(typeof(T), kind);
        }

        public virtual KindMemberDescriptions GetByAttribute(Type type)
        {
            List<MemberDescription> result;
            (_members ?? (_members = new Dictionary<Type, List<MemberDescription>>())).TryGetValue(type, out result);
            return new KindMemberDescriptions(result, MemberKind.All);
        }

        public virtual KindMemberDescriptions GetByAttribute(Type type, MemberKind kind)
        {
            List<MemberDescription> result;
            (_members ?? (_members = new Dictionary<Type, List<MemberDescription>>())).TryGetValue(type, out result);
            return new KindMemberDescriptions(result, kind);
        }

        public virtual MemberDescriptions Members => new MemberDescriptions((_members ?? (_members = new Dictionary<Type, List<MemberDescription>>())));

        public virtual TypeProvider Parent => _parent;

        public virtual DescriptionProvider DescriptionProvider => _provider;

        public virtual Type Type => _type;

        public virtual Type BaseType => _baseType;

        public virtual ICollection<Type> MembersAttributes => (_membersAttributes ?? (_membersAttributes = new HashSet<Type>()));

        public virtual ICollection<Attribute> TypeAttributes => (_typeAttributes ?? (_typeAttributes = new List<Attribute>()));

        public virtual IEnumerable<ConstructorDescription> Constructors
        {
            get { return Members.Where(m => m.Kind == MemberKind.Constructor).Cast<ConstructorDescription>(); }
        }

        public virtual IEnumerable<MethodDescription> Methods
        {
            get { return Members.Where(m => m.Kind == MemberKind.Method).Cast<MethodDescription>(); }
        }

        public virtual IEnumerable<FieldDescription> Fields
        {
            get { return Members.Where(m => m.Kind == MemberKind.Field).Cast<FieldDescription>(); }
        }

        public virtual IEnumerable<PropertyDescription> Properties
        {
            get { return Members.Where(m => m.Kind == MemberKind.Property).Cast<PropertyDescription>(); }
        }

        public virtual IEnumerable<MemberDescription> GetMembers(MemberKind kind)
        {
            return Members.Where(m => m.Kind == kind);
        }

        public virtual ConstructorDescription DefaultConstructor { get; protected set; }
        public virtual ConstructorDescription[] DefaultConstructors { get; protected set; }

        public override int GetHashCode()
        {
            return _index;
        }

        public override bool Equals(object other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (other.GetType() != GetType()) return false;
            return Equals((TypeProvider)other);
        }

        public virtual bool Equals(TypeProvider other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _index == other._index;
        }

        public virtual void Parse(MemberKind kind = MemberKind.All)
        {
            if (!_parsed)
            {
                _parsed = true;
                var provider = DescriptionProvider;
                ParseParentProvider(_type, kind, provider);
                ParseTypeAttributes(_type, kind, provider);
                ParseFields(_type, kind, provider);
                ParseProperties(_type, kind, provider);
                ParseMethods(_type, kind, provider);
                ParseConstructors(_type, kind, provider);
            }
        }

        protected virtual void ParseParentProvider(Type type, MemberKind kind, DescriptionProvider provider)
        {
            var baseType = type.BaseType;
            if (baseType != null && baseType != ObjectType && (BaseType == null || BaseType != baseType))
            {
                _parent = provider.GetProvider(baseType, kind);
            }
        }

        protected virtual void ParseTypeAttributes(Type type, MemberKind kind, DescriptionProvider provider)
        {
            foreach (var customAttribute in type.GetCustomAttributes(true))
            {
                if (provider.IsMappedAttribute(customAttribute.GetType()))
                {
                    (_typeAttributes ?? (_typeAttributes = new List<Attribute>())).Add((Attribute)customAttribute);
                }
            }
        }

        protected virtual void ParseFields(Type type, MemberKind kind, DescriptionProvider provider)
        {
            if ((kind & MemberKind.Field) == MemberKind.Field)
            {
                var fields =
                    type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                                   BindingFlags.GetField | BindingFlags.SetField | BindingFlags.DeclaredOnly);
                var length = fields.Length;
                for (int i = 0; i < length; i++)
                {
                    var field = fields[i];
                    var attributes = field.GetCustomAttributes(true);
                    var attrLen = attributes.Length;
                    for (int j = 0; j < attrLen; j++)
                    {
                        var attribute = attributes[j] as Attribute;
                        Type attributeType;
                        if (attribute != null && provider.IsMappedAttribute(attributeType = attribute.GetType()))
                        {
                            AddDescription(attributeType, new FieldDescription(field, attribute));
                        }
                    }
                }
            }
        }

        protected virtual void ParseProperties(Type type, MemberKind kind, DescriptionProvider provider)
        {
            if ((kind & MemberKind.Property) == MemberKind.Property)
            {
                var properties =
                    type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                                       BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.DeclaredOnly);

                var length = properties.Length;
                for (int i = 0; i < length; i++)
                {
                    var property = properties[i];
                    var attributes = property.GetCustomAttributes(true);
                    var attrLen = attributes.Length;
                    for (int j = 0; j < attrLen; j++)
                    {
                        var attribute = attributes[j] as Attribute;
                        Type attributeType;
                        if (attribute != null && provider.IsMappedAttribute(attributeType = attribute.GetType()))
                        {
                            AddDescription(attributeType, new PropertyDescription(property, attribute));
                        }
                    }
                }
            }
        }

        protected virtual void ParseMethods(Type type, MemberKind kind, DescriptionProvider provider)
        {
            if ((kind & MemberKind.Method) == MemberKind.Method)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                                              BindingFlags.DeclaredOnly);
                var length = methods.Length;
                for (int i = 0; i < length; i++)
                {
                    var method = methods[i];
                    var attributes = method.GetCustomAttributes(true);
                    var attrLen = attributes.Length;
                    for (int j = 0; j < attrLen; j++)
                    {
                        var attribute = attributes[j] as Attribute;
                        Type attributeType;
                        if (attribute != null && provider.IsMappedAttribute(attributeType = attribute.GetType()))
                        {
                            AddDescription(attributeType, new MethodDescription(method, attribute));
                        }
                    }
                }
            }
        }

        protected virtual void ParseConstructors(Type type, MemberKind kind, DescriptionProvider provider)
        {
            if ((kind & MemberKind.Constructor) == MemberKind.Constructor)
            {
                ConstructorInfo[] constructors = type.GetConstructors();
                ConstructorDescription[] constructorDescriptions = new ConstructorDescription[constructors.Length];
                ConstructorInfo constructorToInject = null;
                int maxParameters = int.MaxValue;
                for (var index = 0; index < constructors.Length; index++)
                {
                    ConstructorInfo constructor = constructors[index];
                    var parameters = constructor.GetParameters();
                    object[] attributes = constructor.GetCustomAttributes(true);
                    var attrLen = attributes.Length;
                    for (int i = 0; i < attrLen; i++)
                    {
                        var attribute = attributes[i] as Attribute;
                        Type attributeType;
                        if (attribute != null && provider.IsMappedAttribute(attributeType = attribute.GetType()))
                        {
                            AddDescription(attributeType,
                                new ConstructorDescription(constructor, attribute, parameters));
                        }
                    }

                    if (parameters.Length < maxParameters)
                    {
                        constructorToInject = constructor;
                        maxParameters = parameters.Length;
                    }
                    constructorDescriptions[index] = new ConstructorDescription(constructor, null, parameters);
                }

                DefaultConstructors = constructorDescriptions;
                if (constructorToInject != null)
                {
                    DefaultConstructor = new ConstructorDescription(constructorToInject, null);
                }
            }
        }

        protected void AddDescription(Attribute attribute, MemberDescription description)
        {
            AddDescription(attribute.GetType(), description);
        }

        protected void AddDescription(Type attributeType, MemberDescription description)
        {
            Type type = attributeType;
            List<MemberDescription> result;
            if (!(_members ?? (_members = new Dictionary<Type, List<MemberDescription>>())).TryGetValue(type,
                out result))
            {
                result = new List<MemberDescription>(_memberCapacity);
                (_members ?? (_members = new Dictionary<Type, List<MemberDescription>>())).Add(type, result);
            }

            result.Add(description);
            MapMemberAttribute(type);
        }

        protected virtual void MapMemberAttribute(Type attributeType)
        {
            (_membersAttributes ?? (_membersAttributes = new HashSet<Type>())).Add(attributeType);
        }

        public struct MemberDescriptions : IEnumerable<MemberDescription>
        {
            private readonly Dictionary<Type, List<MemberDescription>> _map;

            public MemberDescriptions(Dictionary<Type, List<MemberDescription>> map)
            {
                _map = map;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_map.Values);
            }

            IEnumerator<MemberDescription> IEnumerable<MemberDescription>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public struct Enumerator : IEnumerator<MemberDescription>
            {
                private Dictionary<Type, List<MemberDescription>>.ValueCollection _values;
                private Dictionary<Type, List<MemberDescription>>.ValueCollection.Enumerator _rootEnumerator;
                private List<MemberDescription> _currentList;
                private List<MemberDescription>.Enumerator _listEnumerator;
                private MemberDescription _current;

                public Enumerator(Dictionary<Type, List<MemberDescription>>.ValueCollection values)
                {
                    _values = values;
                    _rootEnumerator = _values.GetEnumerator();
                    _currentList = default;
                    _current = default;
                    _listEnumerator = default;
                }

                public bool MoveNext()
                {
                    if (_currentList == null)
                    {
                        if (!_rootEnumerator.MoveNext())
                        {
                            return false;
                        }

                        _currentList = _rootEnumerator.Current;
                        _listEnumerator = _currentList.GetEnumerator();
                    }

                    while (true)
                    {
                        var result = _listEnumerator.MoveNext();
                        if (!result)
                        {
                            if (!_rootEnumerator.MoveNext())
                            {
                                return false;
                            }

                            _currentList = _rootEnumerator.Current;
                            _listEnumerator = _currentList.GetEnumerator();
                        }
                        else
                        {
                            _current = _listEnumerator.Current;
                            return true;
                        }
                    }
                }

                public void Reset()
                {
                    _rootEnumerator = _values.GetEnumerator();
                    _currentList = default;
                    _current = default;
                }

                public MemberDescription Current => _current;

                object IEnumerator.Current => _current;

                public void Dispose()
                {
                    _current = default;
                    _currentList = default;
                    _rootEnumerator = default;
                    _values = default;
                }
            }
        }

        public struct KindMemberDescriptions : IEnumerable<MemberDescription>
        {
            private readonly List<MemberDescription> _list;
            private readonly MemberKind _kind;

            public KindMemberDescriptions(List<MemberDescription> list, MemberKind kind)
            {
                _list = list;
                _kind = kind;
            }

            public MemberKind Kind => _kind;
            public int Count => _list == null ? 0 : _list.Count;

            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            IEnumerator<MemberDescription> IEnumerable<MemberDescription>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public struct Enumerator : IEnumerator<MemberDescription>
            {
                private List<MemberDescription> _members;
                private readonly bool _isAll;
                private MemberKind _kind;
                private MemberDescription _current;
                private int _index;

                public Enumerator(KindMemberDescriptions memberDescriptions)
                {
                    _members = memberDescriptions._list;
                    _kind = memberDescriptions._kind;
                    _isAll = _kind == MemberKind.All;
                    _current = default;
                    _index = 0;
                }

                public bool MoveNext()
                {
                    bool result = false;
                    if (_members != null)
                    {
                        if (!_isAll)
                        {
                            while (_index >= 0 && _index < _members.Count &&
                                   (_members[_index].Kind & _kind) != _members[_index].Kind)
                            {
                                _index++;
                            }
                        }

                        if (_index >= 0 && _index < _members.Count)
                        {
                            _current = _members[_index];
                            _index++;
                            result = true;
                        }
                        else
                        {
                            _current = null;
                        }
                    }

                    return result;
                }

                public void Reset()
                {
                    _index = 0;
                    _current = default;
                }

                public MemberDescription Current => _current;

                object IEnumerator.Current => _current;

                public void Dispose()
                {

                }
            }
        }
    }
}