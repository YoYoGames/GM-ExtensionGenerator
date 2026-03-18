using System.Text;

namespace extgen.Emitters.GMCode
{
    public class JSEmitter
    {

        public StringBuilder EmitNative( GMCodeNativeFunction _func )
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat( "{0}[ '{1}' ] = function(", Utils.ModuleName, _func.Name );
            int count = 0;
            foreach( var a in _func.Arguments)
            {
                if (count > 0) sb.Append( ", ");
                sb.AppendFormat( "{0}", a.Name );
                ++count;
            }
            sb.AppendLine( ") {");

            bool fHasReturnType = ((_func.ReturnType != null) && (_func.ReturnType.IDLType != "Unit"));


            List<string> varsForFreeing = new List<string>();
            List<string> argNames = new List<string>();
            foreach( var a in _func.Arguments)
            {
                if (a.Type.IDLType == "String")
                {
                    string namePtr = string.Format( "ptr{0}", varsForFreeing.Count+1 );
                    varsForFreeing.Add( namePtr );
                    sb.AppendFormat( "\tlet {0} = {1}.stringToNewUTF8( {2} );", namePtr, Utils.ModuleName, a.Name );
                    sb.AppendLine();
                    argNames.Add(namePtr);
                }
                else
                {
                    argNames.Add( a.Name );
                }
            }


            sb.Append("\t");
            if (fHasReturnType) {
                if (varsForFreeing.Count > 0) {
                    sb.Append( "let ret = " ); 
                }  
                else {
                    sb.Append( "return " );  
                    fHasReturnType = false;
                } 
            }
            // emit the actual function call for the C++
            sb.AppendFormat( "{0}.__{1}(", Utils.ModuleName, _func.Name );
            count = 0;
            foreach( var a in argNames)
            {
                if (count > 0) sb.Append( ", ");
                sb.AppendFormat( "{0}", a );
                ++count;
            }
            sb.AppendLine( ");" );

            for( int n=varsForFreeing.Count-1; n>=0; --n)
            {
                sb.AppendFormat( "\tfree( {0} );", varsForFreeing[n] );
                sb.AppendLine();
            }


            if (fHasReturnType)
            {
                sb.AppendLine( "\treturn ret; ");
            }

            sb.AppendLine( "}" );

            return sb;            
        }

        public void EmitModule( GMCodeAPI _api, GMCodeModule _mNode, int _depth)
        {
            StringBuilder sbFile = new StringBuilder();

            foreach( var n in _mNode.Natives)
            {
                StringBuilder sbNative = EmitNative( n.Value );
                sbFile.Append( Utils.Indent( _depth+1, sbNative ));
            } // end foreach


            // write out the file
            string filename = Path.ChangeExtension( Path.Combine( _api.DestDirectory, _mNode.Name), ".js" );
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