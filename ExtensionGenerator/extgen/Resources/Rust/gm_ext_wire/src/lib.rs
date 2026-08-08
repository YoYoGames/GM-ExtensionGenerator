//! GameMaker extension wire protocol helpers (Rust port of GMExtWire core tags).

mod buffer;
mod error;
mod tls;

pub use buffer::{GMBufferReader, GMBufferWriter, GMType, GMValue};
pub use error::{clear_last_error, get_last_error_ptr, set_last_error};
pub use tls::store_tls_string;
