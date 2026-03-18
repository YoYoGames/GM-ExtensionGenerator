using System.Security.Cryptography;
using System.Text;

namespace extgen.Emitters.GMCode
{
    public class CPPEmitter
    {

        public StringBuilder EmitNativeSync( GMCodeNativeFunction _func )
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat( "{1} WASM_EXPORT( _{0} )(", _func.Name, _func.ReturnType.GetCPPType() );
            int count=0;
            foreach( var a in _func.Arguments)
            {
                if (count > 0) sb.Append( ", ");
                sb.AppendFormat( "{0} {1}", a.Type.GetCPPType(), a.Name);
                ++count;
            }
            sb.AppendLine( " ) {");
            GMCodeArg argLast = _func.Arguments[ _func.Arguments.Count-1 ];
            sb.AppendFormat( "\tint size = sizeof(BaseNodeChange) + sizeof({0})", argLast.Type.GetCPPType() );
            sb.AppendLine();
            sb.AppendLine( "\tBaseNodeChange* pEntry = (BaseNodeChange*)AllocChangeEntry( size );");
            sb.AppendLine( "\tpEntry->sz = size;");
            sb.AppendFormat( "\tpEntry->pBase = {0};", _func.Arguments[0].Name);
            sb.AppendLine();
            sb.AppendFormat( "\tpEntry->offset = {0};", _func.Data );
            sb.AppendLine();
            sb.AppendFormat( "\t*({0}*)(pEntry+1) = {1};", argLast.Type.GetCPPType(), argLast.Name);
            sb.AppendLine();
            sb.AppendLine("}");

            return sb;
        }

        public StringBuilder EmitNative( GMCodeNativeFunction _func )
        {
            StringBuilder sb = new StringBuilder();
            bool fHasReturnType = ((_func.ReturnType != null) && (_func.ReturnType.IDLType != "Unit"));


            sb.AppendFormat( "{1} WASM_EXPORT( _{0} )(", _func.Name, _func.ReturnType.GetCPPType() );
            int count=0;
            foreach( var a in _func.Arguments)
            {
                if (count > 0) sb.Append( ", ");
                sb.AppendFormat( "{0} _{1}", a.Type.GetCPPType(), a.Name);
                ++count;
            }
            sb.AppendLine( " ) {");
            sb.AppendLine( "\tProcessTStoCPP();");
            sb.Append( "\t");
            if (fHasReturnType)
            {
                sb.AppendFormat( "{0} ret = ", _func.ReturnType.GetCPPType() );
            }
            sb.AppendFormat( "{0}( ", _func.Name);
            count = 0;
            foreach( var a in _func.Arguments)
            {
                if (count > 0) sb.Append( ", ");
                sb.AppendFormat( "_{0}", a.Name);
                ++count;
            }
            sb.AppendLine( " );");
            sb.AppendLine( "\tProcessCPPtoTS();");
            if (fHasReturnType)
            {
                sb.AppendLine( "\treturn ret;" );
            }
            sb.AppendLine("}");

            return sb;
        }
        public void EmitModule( GMCodeAPI _api, GMCodeModule _mNode, int _depth)
        {
            StringBuilder sbFile = new StringBuilder();
            StringBuilder sbNative = null;
            foreach( var n in _mNode.Natives)
            {
                if (n.Value.Name.StartsWith( "Sync_")) {
                    sbNative = EmitNativeSync( n.Value );
                } // end if
                else
                {
                    sbNative = EmitNative( n.Value );                    
                }
                sbFile.Append( Utils.Indent( _depth, sbNative ));
            } // end foreach


            // write out the file
            string filename = Path.ChangeExtension( Path.Combine( _api.DestDirectory, _mNode.Name), ".cpp" );
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