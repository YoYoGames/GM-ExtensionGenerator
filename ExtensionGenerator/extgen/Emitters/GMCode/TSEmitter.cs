using System.Text;
using System.Text.RegularExpressions;

namespace extgen.Emitters.GMCode
{
    public class TSEmitter
    {
        List<GMCodeMethod> Natives = new List<GMCodeMethod>();
        public static string ModuleName = "Module"
;
        public StringBuilder Indent( int _depth, StringBuilder _sb)
        {
            if (_depth == 0) return _sb;
            StringBuilder sb = new StringBuilder();

            var result = Regex.Split(_sb.ToString(), "\r\n|\r|\n");
            foreach( var l in result )
            {
                for( int n=0; n<_depth; ++n)
                {
                    sb.Append( '\t' );
                }
                sb.AppendLine( l );
            }

            return sb;
        }

        public StringBuilder EmitProperty( GMCodeProperty _pNode )
        {
            StringBuilder sb = new StringBuilder();

            string name = _pNode.Name;
            string? type = _pNode.Type.GetTSType();
            string? defaultValue = _pNode.DefaultValue;
            string readOnly = _pNode.ReadOnly ? "readonly " : string.Empty;

            if (defaultValue != null) {
                sb.AppendFormat( "private {3}__{0} : {1} = {2};", name, type,  defaultValue, readOnly);
            } else
            {
                sb.AppendFormat( "private {2}__{0} : {1};", name, type, readOnly); 
            }
            sb.AppendLine();
            
            if (_pNode.Getter)
            {
                sb.AppendFormat( "get {0}() {{ return this.__{0}; }}", name);
                sb.AppendLine();
            }
            if (_pNode.Setter)
            {
                if ( _pNode.Sync )
                    sb.AppendFormat( "set {0}( value : {1}) {{ this.__{0} = value;  {2}.{3}( this.{4}, value ); }}", name, type, ModuleName, _pNode.SyncNative.Name, GMCodeType.PeekSelf() );
                else 
                    sb.AppendFormat( "set {0}( value : {1}) {{ this.__{0} = value; }}", name, type);
                sb.AppendLine();
            }
            

            return sb;            
        } 

        public StringBuilder EmitField( GMCodeField _pNode  )
        {
            StringBuilder sb = new StringBuilder();

            string name = _pNode.Name;
            string? type = _pNode.Type.GetTSType();
            string? defaultValue = _pNode.DefaultValue;
            string readOnly = _pNode.ReadOnly ? "readonly " : string.Empty;

            if (defaultValue != null) {
                sb.AppendFormat( "{3}{0} : {1} = {2};", name, type,  defaultValue, readOnly);
            } // end if
            else
            {
                sb.AppendFormat( "{2}{0} : {1};", name, type, readOnly );
            }

            return sb;            
        } 

        public void AppendFunctionAndArgs( GMCodeMethod _fNode, StringBuilder _sb )
        {
            _sb.AppendFormat( "{0}(", _fNode.Name);
            int count = 0;
            foreach( var a in _fNode.Arguments)
            {
                if (count != 0) _sb.Append( ", ");
                _sb.AppendFormat( "_{0}", a.Name );
                if (a.Optional) _sb.Append( "?");
                string? tsType = a.Type.GetTSType();
                _sb.AppendFormat( " : {0}", tsType );
                if (a.DefaultValue != null)
                {
                    _sb.AppendFormat( " = {0}", a.DefaultValue );
                }
                ++count;
            }
            _sb.AppendFormat( ") ");
        }

        public StringBuilder EmitFunction( GMCodeMethod _fNode, string? _self )
        {
            StringBuilder sb = new StringBuilder();
            AppendFunctionAndArgs( _fNode, sb );
            sb.AppendFormat( ": {0} ", _fNode.ReturnType.GetTSType() );
            sb.Append( "{ " );
            string? native = _fNode.NativeName;
            if (native != null)
            {
                sb.AppendFormat( "{1}.{0}( ", native, ModuleName );
                int count = 0;
                if (_self != null)
                {
                    sb.AppendFormat( "this.{0}", _self );
                    ++count;
                }
                foreach( var a in _fNode.Arguments)
                {
                    if (count != 0) sb.Append( ", ");
                    sb.AppendFormat( "_{0}", a.Name );
                    GMCodeType? argType = (a.Type.IDLType == "Object") ? GMCodeType.Get( GMCodeType.PeekSelfType() ) : a.Type;
                    if ((argType.Class != null) && (argType.Class.SelfName != null))
                    {
                        sb.AppendFormat( ".{0}", argType.Class.SelfName);
                    }
                    ++count;
                } // end foreach
                sb.Append( " );");
            }
            sb.Append( " }" );
            return sb;                        
        }

        public StringBuilder EmitConstructor( GMCodeClass _cNode, GMCodeMethod _fNode, string? _self )
        {
            StringBuilder sb = new StringBuilder();
            AppendFunctionAndArgs( _fNode, sb );
            sb.AppendLine( "{ " );

            // check for super class
            if ((_cNode.InheritsFromClass != null) && (_cNode.InheritsFromClass.Count > 0))
            {
                GMCodeClass superClass = _cNode.InheritsFromClass[0];
                if (superClass.Constructor != null) {
                    sb.Append( "\tsuper(" );
                    int count = 0;
                    if (_self != null)
                    {
                        sb.AppendFormat( "this.{0}", _self );
                        ++count;
                    }
                    foreach( var a in superClass.Constructor.Arguments)
                    {
                        if (count != 0) sb.Append( ", ");
                        sb.AppendFormat( "_{0}", a.Name );
                        GMCodeType? argType = (a.Type.IDLType == "Object") ? GMCodeType.Get( GMCodeType.PeekSelfType() ) : a.Type;
                        if ((argType.Class != null) && (argType.Class.SelfName != null))
                        {
                            sb.AppendFormat( ".{0}", argType.Class.SelfName);
                        }
                            ++count;
                    } // end foreach
                    sb.AppendLine( ");");
                } // end if
            }

            // check for fields or properties that we are going to initialise from the arguments
            foreach( var a in _fNode.Arguments)
            {
                GMCodeField field = null;
                if (_cNode.Fields.TryGetValue(a.Name, out field)) {
                    sb.AppendFormat( "\tthis.{0} = _{0};", a.Name);
                    sb.AppendLine();
                } // end if
                GMCodeProperty prop = null;
                if (_cNode.Properties.TryGetValue(a.Name, out prop)) {
                    sb.AppendFormat( "\tthis.__{0} = _{0};", a.Name);
                    sb.AppendLine();
                } // end if
            } // end foreach

            // output the Native call 
            string? native = _fNode.NativeName;
            if (native != null)
            {
                sb.AppendFormat( "\tthis.{1} = {2}.{0}( ", native,  GMCodeType.PeekSelf(), ModuleName );
                int count = 0;
                if (_self != null)
                {
                    sb.AppendFormat( "this.{0}", _self );
                    ++count;
                }
                foreach( var a in _fNode.Arguments)
                {
                    if (count != 0) sb.Append( ", ");
                    sb.AppendFormat( "_{0}", a.Name );
                    GMCodeType? argType = (a.Type.IDLType == "Object") ? GMCodeType.Get( GMCodeType.PeekSelfType() ) : a.Type;
                    if ((argType.Class != null) && (argType.Class.SelfName != null))
                    {
                        sb.AppendFormat( ".{0}", argType.Class.SelfName);
                    }
                   ++count;
                } // end foreach
                sb.AppendLine( " );");
            }
            sb.Append( "}" );
            return sb;                        
        }

        public StringBuilder EmitClass( GMCodeClass _cNode, int _depth  )
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat( "class {0} ",  _cNode.Name );
            int nPopSelf = 0;
            if ((_cNode.InheritsFrom != null) && (_cNode.InheritsFrom.Count > 0))
            {
                sb.AppendFormat( " extends {0} ", _cNode.InheritsFrom[0] );
                GMCodeClass? curr = _cNode.InheritsFromClass[0];
                while( curr != null)
                {
                    if (curr.SelfName != null) {
                        ++nPopSelf;
                        GMCodeType.PushSelf( curr.SelfName, curr.Name );                        
                    } // end if
                    curr = ((curr.InheritsFrom != null) && (curr.InheritsFrom.Count > 0)) ? curr.InheritsFromClass[0] : null;
                } // end while
            }
            sb.AppendLine( "{");
            if (_cNode.SelfName != null) { 
                ++nPopSelf;
                GMCodeType.PushSelf( _cNode.SelfName, _cNode.Name );
            } // end if


            foreach( var p in _cNode.Fields )
            {
                var functionSB = EmitField( p.Value );
                sb.Append( Indent( 1, functionSB).ToString() );
            }            
            sb.AppendLine();
            foreach( var p in _cNode.Properties )
            {
                var functionSB = EmitProperty( p.Value );
                sb.Append( Indent( 1, functionSB).ToString() );
            }            
            sb.AppendLine();
            foreach( var f in _cNode.Methods )
            {

                var functionSB = EmitFunction( f.Value, _cNode.SelfName );
                sb.Append( Indent( 1, functionSB).ToString() );
            }            

            if (_cNode.Constructor != null)
            {
                var functionSB = EmitConstructor( _cNode, _cNode.Constructor, _cNode.SelfName );
                sb.Append( Indent( 1, functionSB).ToString() );                
            }

            while (nPopSelf > 0) {
                GMCodeType.PopSelf();
                --nPopSelf;
            } // end while
            sb.Append( "}" );
            return Indent( _depth, sb);
        }

        private StringBuilder EmitInterface( GMCodeNativeFunction _m)
        {
            StringBuilder _sb = new StringBuilder();
            _sb.AppendFormat( "interface {0} {{(", _m.Name);
            int count = 0;
            foreach( var a in _m.Arguments)
            {
                if (count != 0) _sb.Append( ", ");
                _sb.AppendFormat( "_{0} : {1}", a.Name, a.Type.GetTSType() );
                GMCodeType? argType =  a.Type;
                ++count;
            } // end foreach
            GMCodeType retType = _m.ReturnType;
            _sb.AppendFormat( ") : {0}", retType.GetTSType() );
            _sb.Append( "}");
            return _sb;
        }

        public void EmitModule( GMCodeAPI _api, GMCodeModule _mNode, int _depth)
        {
            StringBuilder sb = new StringBuilder();

            // do all the classes 
            foreach( var c in _mNode.Classes )
            {
                var classSB = EmitClass( c.Value, _depth+1 );
                sb.AppendLine( classSB.ToString() );
            }


            // output everything
            StringBuilder sbFile = new StringBuilder();
            sbFile.AppendFormat( "namespace {0} ", _mNode.Name  );
            sbFile.AppendLine( "{" );

            // add the interfaces
            sbFile.AppendLine();
            foreach( var ni in _mNode.Natives)
            {
                StringBuilder sbInterface = EmitInterface( ni.Value );
                sbFile.Append( Indent( _depth+1, sbInterface ));
            } 
            sbFile.AppendLine();
            // add the module entries
            sbFile.AppendFormat( "\tinterface {0}Funcs", _mNode.Name );
            sbFile.AppendLine();
            sbFile.AppendLine("\t{");
            foreach( var ni in _mNode.Natives)
            {
                sbFile.AppendFormat( "\t\t{0} : {0};", ni.Value.Name );
                sbFile.AppendLine();
            } 
            sbFile.AppendLine("\t}");
            sbFile.AppendFormat( "\tdeclare var {1} : {0}Funcs;", _mNode.Name, ModuleName);
            sbFile.AppendLine();
            sbFile.AppendLine();

            // add the classes
            sbFile.Append( sb.ToString() );

            sbFile.AppendLine( "}" );


            // write out the file
            string filename = Path.ChangeExtension( Path.Combine( _api.DestDirectory, _mNode.Name), ".ts" );
            File.WriteAllBytes( filename, UTF8Encoding.UTF8.GetBytes(sbFile.ToString()));
        }

        public void EmitDatabase( GMCodeAPI _db )
        {
            // do every module
            foreach( var m in _db.Modules )
            {
                EmitModule( _db, m.Value, 0 );
            }  // end foreach         
        }
    }

}