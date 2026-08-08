use std::cell::RefCell;
use std::ffi::{c_char, CString};

thread_local! {
    static TLS_RETURN: RefCell<Option<CString>> = const { RefCell::new(None) };
}

/// Store a string for GameMaker to copy immediately after the native call returns.
pub fn store_tls_string(s: String) -> *const c_char {
    let c = CString::new(s).unwrap_or_else(|_| CString::new("<invalidutf8>").unwrap());
    let p = c.as_ptr();
    TLS_RETURN.with(|cell| {
        *cell.borrow_mut() = Some(c);
    });
    p
}
