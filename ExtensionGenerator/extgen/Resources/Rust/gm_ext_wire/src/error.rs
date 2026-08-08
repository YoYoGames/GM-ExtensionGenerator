use std::cell::RefCell;
use std::ffi::{c_char, CString};

thread_local! {
    static TLS_LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

pub fn set_last_error(msg: &str) {
    let c = CString::new(msg).unwrap_or_else(|_| CString::new("<invalid utf8>").unwrap());
    TLS_LAST_ERROR.with(|cell| {
        *cell.borrow_mut() = Some(c);
    });
}

pub fn get_last_error_ptr() -> *const c_char {
    TLS_LAST_ERROR.with(|cell| {
        match cell.borrow().as_ref() {
            Some(c) => c.as_ptr(),
            None => std::ptr::null(),
        }
    })
}

pub fn clear_last_error() {
    TLS_LAST_ERROR.with(|cell| {
        *cell.borrow_mut() = None;
    });
}
