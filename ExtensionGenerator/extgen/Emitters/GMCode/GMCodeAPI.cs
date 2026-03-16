using gmidlreader;

namespace extgen.Emitters.GMCode
{

    public class GMCodeType
    {
        public string IDLType { get; set; }
        public GMCodeClass? Class { get; set; }
        public GMCodeType? RefType { get; set; }

        public GMCodeType( string _idlType )
        {
            IDLType = _idlType;
            RefType = null;
            Class = null;
        }

        public GMCodeType( string _idlType, GMCodeType _type )
        {
            IDLType = _idlType;
            RefType = _type;
            Class = null;
        }

        public static Dictionary<string, GMCodeType > types = new Dictionary<string, GMCodeType>();
        static Stack<string> selfStack = new Stack<string>();
        static Stack<string> selfStackType = new Stack<string>();
        public static GMCodeType Get( string _typeIDL, string? _typeAttr=null)
        {
            GMCodeType t = null;
            if (!types.TryGetValue( _typeIDL, out t))
            {
                if ((_typeIDL == "Array") && (_typeAttr != null))
                {
                    GMCodeType r = Get( _typeAttr, null );
                    t = new GMCodeType( _typeIDL, r );
                } // end if
                else {
                    t = new GMCodeType( _typeIDL ); 
                } // end else
                types.Add( t.IDLType, t );
            }

            return t;            
        }

        public static void PushSelf( string _self, string _typeSelf ) { selfStack.Push(_self); selfStackType.Push( _typeSelf );}
        public static void PopSelf() { selfStack.Pop(); selfStackType.Pop(); }
        public static string PeekSelf() { return selfStack.Peek(); }
        public static string? PeekSelfType() { return selfStackType.Count > 0 ? selfStackType.Peek() : null; }

        public string GetTSType()
        {
            string ret;
            switch( IDLType ) {
            case "String": ret = "string"; break;
            case "Double": ret = "number"; break;
            case "Array": ret = "[]"; break;
            case "Int64" : ret = "int64"; break;
            case "Int32" : ret = "int32"; break;
            case "Bool" : ret = "boolean"; break;
            case "Object" :  ret = (selfStackType.Count>0) ? string.Format( "{0}", selfStackType.Peek()) : "null"; break;
            case "Unit" : ret = "void"; break;
            default: ret = IDLType; break;
            } // end switch
            return ret;            
        }
    }

    public class GMCodeArg
    {
        public string Name { get; private set; }        
        public GMCodeType Type { get; private set; }
        public string? DefaultValue { get; private set; }
        public bool Optional { get; private set; }

        public GMCodeArg( string _name, GMCodeType _type, bool _optional, string? _default = null )
        {
            Name = _name;
            Type = _type;
            Optional = _optional;
            DefaultValue = _default;
        }
    }

    public class GMCodeField
    {
        public string Name { get; private set; }        
        public GMCodeType Type { get; private set; }
        public string? DefaultValue { get; private set; }
        public bool ReadOnly { get; private set; }
        public bool Getter { get; private set; }
        public bool Setter {get; private set; }
        public bool Sync { get; private set; }
        public GMCodeNativeFunction SyncNative { get; set; }

        public GMCodeField( string _name, GMCodeType _type, bool _sync, string? _default = null, bool? _readOnly = null, bool? _get = null, bool? _set = null  )
        {
            Name = _name;
            Type = _type;
            DefaultValue = _default;
            ReadOnly = (_readOnly != null) ? (bool)_readOnly : false;
            Getter = (_get != null) ? (bool)_get : false;
            Setter = (_set != null) ? (bool)_set : false;
            Sync = _sync;
        }
    }

     public class GMCodeProperty
    {
        public string Name { get; private set; }        
        public GMCodeType Type { get; private set; }
        public string? DefaultValue { get; private set; }
        public bool ReadOnly { get; private set; }
        public bool Getter { get; private set; }
        public bool Setter {get; private set; }
        public bool Sync { get; private set; }
        public GMCodeNativeFunction SyncNative { get; set; }

        public GMCodeProperty( string _name, GMCodeType _type, bool _sync, string? _default = null, bool? _readOnly = null, bool? _get = null, bool? _set = null  )
        {
            Name = _name;
            Type = _type;
            DefaultValue = _default;
            ReadOnly = (_readOnly != null) ? (bool)_readOnly : false;
            Getter = (_get != null) ? (bool)_get : false;
            Setter = (_set != null) ? (bool)_set : false;
            Sync = _sync;
        }
    }

    public class GMCodeNativeFunction
    {
        public string Name { get; private set; }        
        public GMCodeType ReturnType { get; private set; }
        public List<GMCodeArg> Arguments { get; private set; }

        public GMCodeNativeFunction( string _name, GMCodeType _returnType)
        {
            Name = _name;
            ReturnType = _returnType;
            Arguments = new List<GMCodeArg>();
        }
    }

    public class GMCodeMethod
    {
        public string Name { get; private set; }        
        public GMCodeType ReturnType { get; private set; }
        public List<GMCodeArg> Arguments { get; private set; }
        public string? NativeName { get; private set; }

        public GMCodeMethod( string _name, GMCodeType _type, string? _nativeName )
        {
            Name = _name;
            ReturnType = _type;
            NativeName = _nativeName;
            Arguments = new List<GMCodeArg>();
        }
    }

    public class GMCodeClass
    {
        public string Name { get; set; }
        public List<string> InheritsFrom { get; set; }
        public List<GMCodeClass> InheritsFromClass { get; set; }

        public string? SelfName { get; set; }

        public Dictionary<string, GMCodeField> Fields { get; set; }
        public Dictionary<string, GMCodeProperty> Properties { get; set; }
        public Dictionary<string, GMCodeMethod> Methods { get; set; }
        public GMCodeMethod? Constructor { get; set; }
        public bool Sync { get; set; }

        public GMCodeClass( string _name )
        {
            Name = _name;
            InheritsFrom = new List<string>();
            InheritsFromClass = new List<GMCodeClass>();
            SelfName = null;
            Fields = new Dictionary<string, GMCodeField>();
            Properties = new Dictionary<string, GMCodeProperty>();
            Methods = new Dictionary<string, GMCodeMethod>();
            Sync = false;
        }
    }

    public class GMCodeModule
    {
        public string Name { get; private set; }
        public Dictionary<string, GMCodeClass>  Classes  {get; private set;} 
        public Dictionary<string, GMCodeNativeFunction> Natives { get; private set; }     

        public GMCodeModule( string _name )
        {
            Name = _name;
            Classes = new Dictionary<string, GMCodeClass>();
            Natives = new Dictionary<string, GMCodeNativeFunction>();
        }
    }
    public class GMCodeAPI
    {
        public string DestDirectory { get; private set; }

        public Dictionary<string, GMCodeModule> Modules { get; private set; }
        public GMCodeModule m_currModule;

        public GMCodeAPI( string _destDirectory )
        {
            DestDirectory = _destDirectory;
            Modules = new Dictionary<string, GMCodeModule>();
        }

        private GMCodeMethod GatherMethod( GMIDLNode<GMIDLFunction> _fNode, string? _self )
        {
            string? nativeName = _fNode.Attributes.GetAsString( "native" );
            GMCodeType returnType = GMCodeType.Get( _fNode.Data.ReturnType.ToString(), _self );
            GMCodeMethod ret = new GMCodeMethod( _fNode.Name, returnType, nativeName  );

            int count = 0;
            foreach( var a in _fNode.Data.NamedArgs )
            {
                string argTypeNameKey = string.Format( "arg{0}type", count );
                string argType = a.Attributes.GetAsString( argTypeNameKey ) ?? a.Data.Type.ToString();

                GMCodeArg arg = new GMCodeArg( a.Name, GMCodeType.Get( argType, _self ), a.Data.Optional, a.Data.Default );
                ret.Arguments.Add( arg );
                ++count;
            }
            return ret;
        }

        private GMCodeField GatherField( GMIDLNode<GMIDLProperty> _pNode, bool _fClassSync )
        {
            string? defaultValue = _pNode.Attributes.GetAsString( "default" );
            GMCodeField ret = new GMCodeField( _pNode.Name, 
                                                GMCodeType.Get( _pNode.Data.Type.ToString(), 
                                                _pNode.Attributes.GetAsString( "type" ) ), 
                                                _fClassSync || _pNode.Attributes.ContainsKey( "sync" ),
                                                defaultValue, 
                                                _pNode.Data.Setter == null, 
                                                _pNode.Data.Getter != null, 
                                                _pNode.Data.Setter != null ); 

            return ret;
        }

        private GMCodeProperty GatherProperty( GMIDLNode<GMIDLProperty> _pNode, bool _fClassSync )
        {
            string? defaultValue = _pNode.Attributes.GetAsString( "default" );
            GMCodeProperty ret = new GMCodeProperty( _pNode.Name, 
                                                    GMCodeType.Get( _pNode.Data.Type.ToString(), 
                                                    _pNode.Attributes.GetAsString( "type" ) ), 
                                                _fClassSync || _pNode.Attributes.ContainsKey( "sync" ),
                                                    defaultValue,
                                                    _pNode.Data.Setter == null,
                                                    _pNode.Data.Getter != null, 
                                                    _pNode.Data.Setter != null ); 
            return ret;
        }

        private GMCodeClass GatherClass( GMIDLNode<GMIDLClass> _cNode)
        {
            GMCodeClass ret = new GMCodeClass( _cNode.Name );
            ret.Sync = _cNode.Attributes.ContainsKey( "sync" );

            // get the prototype
            if (_cNode.Data.Prototype != null)
            {
                ret.InheritsFrom.Add( _cNode.Data.Prototype );
            }

            // find any properties (or fields) that are being set as the self (to use in native calls)
            string? self = null;
            foreach( var p in _cNode.Data.Properties )
            {
                self = p.Attributes.ContainsKey( "self" ) ? p.Name : null;
                if (self != null) break;
            } 
            ret.SelfName = self;

            foreach( var p in _cNode.Data.Properties )
            {
                if (p.Attributes.ContainsKey( "field"))
                {
                    GMCodeField field = GatherField( p, ret.Sync );
                    ret.Fields.Add( field.Name, field);
                }
                else
                {
                    GMCodeProperty prop = GatherProperty( p, ret.Sync );
                    ret.Properties.Add( prop.Name, prop );
                }
            } 

            foreach( var f in _cNode.Data.Functions )
            {
                GMCodeMethod method = GatherMethod( f, ret.SelfName );
                ret.Methods.Add( method.Name, method );  
            } 

            if (_cNode.Data.Constructor != null)
            {
                GMCodeMethod method = GatherMethod( _cNode.Data.Constructor, ret.SelfName  );
                ret.Constructor = method;                  
            }

            return ret;
        } // end GatherCloss

        private GMCodeModule GatherModule( GMIDLDatabase _db, GMIDLNode<GMIDLModule> _mNode)
        {
            GMCodeModule module = new GMCodeModule( _mNode.Name );
            m_currModule = module;

            // gather classes and functions (at global scope)
            foreach( var c in _mNode.Data.Classes )
            {
                GMCodeClass cls = GatherClass( c );
                module.Classes.Add( cls.Name, cls );
            }

            return module;

        } 

        private void ResolveClass( GMCodeModule _m, GMCodeClass _c )
        {
            foreach( var i in _c.InheritsFrom )
            {
                GMCodeClass superClass = null;
                if (_m.Classes.TryGetValue( i, out superClass ))
                {
                    _c.InheritsFromClass.Add( superClass );
                }
                else
                {
                    Console.Error.WriteLine( "Unable to find super class {0} for class {1}", i, _c.Name);
                }
            }
        }

        private void ResolveModule( GMCodeModule _m)
        {
            foreach( var c in _m.Classes)
            {
                ResolveClass( _m, c.Value );
            }
        }

        private GMCodeClass FindClassInModule( string _name, GMCodeModule _m)
        {
            GMCodeClass ret = null;
            if (_m.Classes.TryGetValue( _name, out ret))
            {
                
            }
            return ret;
        }

        private void GatherSyncNativeFromProperty( GMCodeModule _m, GMCodeClass _c, GMCodeProperty _prop)
        {
            GMCodeNativeFunction nativeFunc = null;

            string selfTypeString = GMCodeType.PeekSelfType();
            GMCodeType selfType = GMCodeType.Get( selfTypeString );            
            GMCodeType propType = _prop.Type;

            string nameNative = string.Format( "Sync_{0}_{1}", _c.Name, _prop.Name);

            nativeFunc = new GMCodeNativeFunction( nameNative, GMCodeType.Get("void"));
            _m.Natives.Add( nativeFunc.Name, nativeFunc );
            _prop.SyncNative = nativeFunc;

            if (selfType != null) {
                GMCodeArg arg = new GMCodeArg( "_self", selfType, false, string.Empty);
                nativeFunc.Arguments.Add( arg );
            } 

            GMCodeArg argV = new GMCodeArg( "value", propType, false, string.Empty);
            nativeFunc.Arguments.Add( argV );

        }

        private void GatherNativeFromMethod( GMCodeModule _m, GMCodeClass _c, GMCodeMethod _method)
        {
            // lets get the native function setup 
            GMCodeNativeFunction nativeFunc = null;
            if (_method.NativeName != null)
            {
                string selfTypeString = GMCodeType.PeekSelfType();
                GMCodeType selfType = GMCodeType.Get( selfTypeString );

                GMCodeType returnType = (_method.Name == "constructor") ? selfType : _method.ReturnType;
                nativeFunc = new GMCodeNativeFunction( _method.NativeName, returnType);
                _m.Natives.Add( nativeFunc.Name, nativeFunc );

                if (selfType != null) {
                    GMCodeArg arg = new GMCodeArg( "_self", selfType, false, string.Empty);
                    nativeFunc.Arguments.Add( arg );
                } 

                // do all the arguments as well
                int count = 0;
                foreach( var a in _method.Arguments )
                {
                    nativeFunc.Arguments.Add( a );
                    ++count;
                }
            }   

        } 

        private void GatherNativeFromClass( GMCodeModule _m, GMCodeClass _c)
        {
            int nPopSelf = 0;
            if ((_c.InheritsFrom != null) && (_c.InheritsFrom.Count > 0))
            {
                GMCodeClass? curr = _c.InheritsFromClass[0];
                while( curr != null)
                {
                    if (curr.SelfName != null) {
                        ++nPopSelf;
                        GMCodeType.PushSelf( curr.SelfName, curr.Name );                        
                    } // end if
                    curr = ((curr.InheritsFrom != null) && (curr.InheritsFrom.Count > 0)) ? curr.InheritsFromClass[0] : null;
                } // end while
            }
            if (_c.SelfName != null) { 
                ++nPopSelf;
                GMCodeType.PushSelf( _c.SelfName, _c.Name );
            } // end if

            foreach(  var m in _c.Methods )
            {
                GatherNativeFromMethod( _m, _c, m.Value );
            }
            if (_c.Constructor != null)
                GatherNativeFromMethod( _m, _c, _c.Constructor );


            // look for all the sync properties and we need to generate a native for them to do the Sync
            foreach( var p in _c.Properties)
            {
                GatherSyncNativeFromProperty( _m, _c, p.Value );
            }
        }
        private void GatherNativeFromModules( GMCodeModule _m)
        {
            foreach(  var c in _m.Classes )
            {
                GatherNativeFromClass( _m, c.Value );
            }
        }

        public void ProcessIDL( GMIDLDatabase _db )
        {
            // gather all the types, class's and functions

            // do every module
            foreach( var m in _db.Modules )
            {
                // only consider modules that are marked as a `gmcode_api`
                if (m.Attributes.ContainsKey( "gmcode_api")) {
                    GMCodeModule module = GatherModule(_db, m );
                    Modules.Add( module.Name, module );
                } // end if
            }  // end foreach    
            m_currModule = null;

            // we need to ensure that we resolve all the names of the inherits from into actual clases
            foreach( var m in Modules)
            {
                ResolveModule( m.Value );
            }

            // lets turn all the types that are named into classes
            foreach( var t in GMCodeType.types)
            {
                GMCodeClass c = null;
                foreach( var m in Modules)
                {
                    c = FindClassInModule( t.Key, m.Value );
                    if (c != null) break;
                }

                if (c != null)
                    t.Value.Class = c;                
            }

            // now we have types setup properly lets gather all the native functions and ensure that they are setup with the types properly
            foreach( var m in Modules)
            {
                GatherNativeFromModules( m.Value );
            }



            TSEmitter ts = new TSEmitter();
            ts.EmitDatabase( this );


        }
        
    }
}