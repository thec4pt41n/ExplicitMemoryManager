using System;
using System.Collections.Concurrent;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TakeOutTheGarbage
{
    public static unsafe class ExplicitMemoryManager
    {
        // Provide ongoing lookup table of types
        private static readonly ConcurrentDictionary<Type, int> _backingStore = new ConcurrentDictionary<Type, int>();

        /// <summary>
        /// Allocates a block of size bytes of memory, returning a pointer to the beginning of the block.
        /// The content of the newly allocated block of memory is not initialized, remaining with indeterminate values.
        ///
        /// If size is zero, the return value is a null pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="size"></param>
        /// <returns></returns>
        public static void* malloc<T>(int size) where T : struct
        {
            if (size == 0) return null;

            return Marshal.AllocHGlobal(SizeOfType<T>(size)).ToPointer();
        }

        /// <summary>
        /// Allocates a block of memory for an array of num elements, each of them size bytes long,
        /// and initializes all its bits to zero.
        /// The effective result is the allocation of a zero-initialized memory block of (num*size) bytes.
        ///
        /// If size is zero, the return value is a null pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="size"></param>
        /// <returns></returns>        
        public static void* calloc<T>(int size) where T : struct
        {
            if (size == 0) return null;

            int sizeInBytes = SizeOfType<T>(size);
            byte* newArrayPointer = (byte*)Marshal.AllocHGlobal(sizeInBytes).ToPointer();

            for (int i = 0; i < sizeInBytes; i++)
                *(newArrayPointer + i) = 0;

            return newArrayPointer;
        }

        /// <summary>
        /// Changes the size of the memory block pointed to by ptr.
        /// The function may move the memory block to a new location (whose address is returned by the function).
        /// The content of the memory block is preserved up to the lesser of the new and old sizes.
        ///  If the new size is larger, the value of the newly allocated portion is indeterminate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static void* realloc<T>(void* ptr, int size) where T : struct
        {
            if (ptr == null) malloc<T>(size);

            int sizeInBytes = SizeOfType<T>(size);

            return (Marshal.ReAllocHGlobal(new IntPtr(ptr), new IntPtr(sizeInBytes))).ToPointer();
        }

        /// <summary>
        /// A block of memory previously allocated by a call to malloc, calloc or realloc is deallocated,
        /// making it available again for further allocations.
        ///
        /// If ptr is a null pointer, the function does nothing.
        /// </summary>
        /// <param name="ptr"></param>
        public static void free(void* ptr)
        {
            if (ptr == null) return;

            Marshal.FreeHGlobal(new IntPtr(ptr));
        }

        /// <summary>
        /// Use the MSIL SizeOf, with support for generics and no padding.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="numberOfElements"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SizeOfType<T>(int numberOfElements)
        {

            return _backingStore.GetOrAdd(typeof(T), t2 =>
            {

                var dm = new DynamicMethod("#SizeOf#", typeof(int), Type.EmptyTypes);
                dm.GetILGenerator().Emit(OpCodes.Sizeof, t2);
                dm.GetILGenerator().Emit(OpCodes.Ret);

                var ILSizeOf = (Func<int>)dm.CreateDelegate(typeof(Func<int>));
                return ILSizeOf() * numberOfElements;
            });
        }
    }
}
