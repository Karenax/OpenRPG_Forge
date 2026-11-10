use std::any::Any;

macro_rules! erased {
    ($name:ident $(+ $bound:path)?) => {
        /// A type-erased item with checked access to its original type.
        pub struct $name {
            value: Box<dyn Any $(+ $bound)?>,
        }

        impl $name {
            /// Create a new type-erased item.
            pub fn new<T: Any $(+ $bound)?>(item: T) -> Self {
                Self {
                    value: Box::new(item),
                }
            }

            /// Get a reference to the inner value.
            pub fn get_ref<T: 'static>(&self) -> &T {
                self.value.downcast_ref().expect("Erased: type mismatch")
            }

            /// Get a mutable reference to the inner value.
            pub fn get_mut<T: 'static>(&mut self) -> &mut T {
                self.value.downcast_mut().expect("Erased: type mismatch")
            }

            /// Consume the item and return the inner value.
            pub fn into_inner<T: 'static>(self) -> T {
                *self.value.downcast().expect("Erased: type mismatch")
            }
        }
    };
}

erased!(Erased + Send);
erased!(ErasedLocal);
