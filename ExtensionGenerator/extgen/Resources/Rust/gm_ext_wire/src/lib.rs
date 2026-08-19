//! GameMaker extension wire protocol helpers (Rust port of GMExtWire core tags).

mod buffer;
mod dispatch;
mod error;
mod function;
mod handle_buffer;
mod stream;
mod tls;

pub use buffer::{
    GMBufferReader, GMBufferWriter, GMSliceWriter, GMType, GMValue, GMValueOwned, GrowableWireWriter,
    TypedStructDecoder, TypedStructOwnedDecoder, WireByteWriter,
};
pub use dispatch::DispatchQueue;
pub use error::{clear_last_error, get_last_error_ptr, set_last_error};
pub use function::GMFunction;
pub use handle_buffer::{BufferQueue, GMBuffer};
pub use stream::{ArrayStream, DataStream, GmStruct, StructStream};
pub use tls::store_tls_string;
