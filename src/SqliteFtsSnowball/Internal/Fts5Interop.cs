#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Snowball;

namespace SqliteFtsSnowball.Internal
{
    /// <summary>
    /// The low-level FTS5 interop that registers the Snowball tokenizer and bridges SQLite's
    /// C tokenizer callbacks to managed code.
    /// </summary>
    /// <remarks>
    /// The implementation is pure P/Invoke so it works with whatever SQLite build the host
    /// app already ships (via SQLitePCLRaw). The tokenizer logic is fully managed, so a single
    /// platform-neutral assembly runs everywhere, including AOT targets such as iOS.
    /// </remarks>
    internal static unsafe class Fts5Interop
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ERROR = 1;

        /// <summary>The DllImport name; the resolver maps it to <see cref="SqliteLibraryName"/>.</summary>
        private const string Lib = "e_sqlite3";

        internal static string SqliteLibraryName = "e_sqlite3";

        private static readonly object Gate = new();
        private static readonly IntPtr Fts5ApiPtrType = AllocPersistentUtf8("fts5_api_ptr");
        private static IntPtr _vtable;

        [ThreadStatic]
        private static Exception? _lastError;

        internal static Exception? LastError => _lastError;

        static Fts5Interop()
        {
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(Fts5Interop).Assembly, Resolve);
            }
            catch
            {
                // A resolver can only be set once per assembly; ignore if already set.
            }
        }

        // ---------------------------------------------------------------------
        // Native library resolution
        // ---------------------------------------------------------------------
        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!string.Equals(libraryName, Lib, StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            // For the default provider, defer to the runtime's normal resolution so our
            // P/Invokes bind to exactly the same native SQLite as the app's SQLitePCLRaw
            // provider. This is critical on iOS: a custom resolver using the main program
            // handle would bind "e_sqlite3" to the system libsqlite3 instead of the bundled
            // e_sqlite3, which crashes in sqlite3_bind_pointer.
            if (string.Equals(SqliteLibraryName, Lib, StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            // Custom provider name (e.g. winsqlite3, sqlcipher): load it explicitly.
            return NativeLibrary.TryLoad(SqliteLibraryName, assembly, searchPath, out IntPtr handle)
                ? handle
                : IntPtr.Zero;
        }

        // ---------------------------------------------------------------------
        // Native methods (resolved against the host app's SQLite build)
        // ---------------------------------------------------------------------
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nByte, out IntPtr stmt, IntPtr pzTail);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_pointer")]
        private static extern int sqlite3_bind_pointer(IntPtr stmt, int index, IntPtr pointer, IntPtr type, IntPtr destructor);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
        private static extern int sqlite3_finalize(IntPtr stmt);

        // ---------------------------------------------------------------------
        // Registration
        // ---------------------------------------------------------------------
        internal static void Register(IntPtr db, string name, SnowballTokenizerOptions options)
        {
            if (options.StemmingEnabled && !IsNoStem(options.Language) && !SnowballStemmers.IsSupported(options.Language))
            {
                throw new SnowballTokenizerException($"Unsupported Snowball language '{options.Language}'.");
            }

            lock (Gate)
            {
                IntPtr api = GetFts5Api(db);

                // fts5_api layout: { int iVersion; (*xCreateTokenizer)(); ... }. With natural
                // alignment the first function pointer sits exactly one pointer-width in.
                IntPtr xCreateTokenizer = Marshal.ReadIntPtr(api, IntPtr.Size);
                if (xCreateTokenizer == IntPtr.Zero)
                {
                    throw new SnowballTokenizerException("fts5_api.xCreateTokenizer is null.");
                }

                var context = new RegistrationContext(options, AllocPersistentUtf8(name));
                GCHandle contextHandle = GCHandle.Alloc(context);

                var createTokenizer = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int>)xCreateTokenizer;
                int rc = createTokenizer(
                    api,
                    context.NamePtr,
                    GCHandle.ToIntPtr(contextHandle),
                    GetVtable(),
                    (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&XDestroyContext);

                if (rc != SQLITE_OK)
                {
                    Marshal.FreeHGlobal(context.NamePtr);
                    contextHandle.Free();
                    throw new SnowballTokenizerException($"fts5 xCreateTokenizer('{name}') failed with code {rc}.");
                }
            }
        }

        private static IntPtr GetVtable()
        {
            if (_vtable == IntPtr.Zero)
            {
                IntPtr vtable = Marshal.AllocHGlobal(IntPtr.Size * 3);
                Marshal.WriteIntPtr(vtable, 0 * IntPtr.Size, (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, IntPtr*, int>)&XCreate);
                Marshal.WriteIntPtr(vtable, 1 * IntPtr.Size, (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&XDelete);
                Marshal.WriteIntPtr(vtable, 2 * IntPtr.Size, (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, IntPtr, int, IntPtr, int>)&XTokenize);
                _vtable = vtable;
            }

            return _vtable;
        }

        private static IntPtr GetFts5Api(IntPtr db)
        {
            byte[] sql = Encoding.UTF8.GetBytes("SELECT fts5(?1)\0");
            int rc = sqlite3_prepare_v2(db, sql, -1, out IntPtr stmt, IntPtr.Zero);
            if (rc != SQLITE_OK || stmt == IntPtr.Zero)
            {
                throw new SnowballTokenizerException(
                    $"Could not obtain the FTS5 API (sqlite3_prepare_v2 rc={rc}). " +
                    "Ensure the SQLite build has FTS5 enabled (SQLitePCLRaw bundle_e_sqlite3 does).");
            }

            IntPtr storage = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(storage, IntPtr.Zero);
                rc = sqlite3_bind_pointer(stmt, 1, storage, Fts5ApiPtrType, IntPtr.Zero);
                if (rc != SQLITE_OK)
                {
                    throw new SnowballTokenizerException($"sqlite3_bind_pointer failed (rc={rc}).");
                }

                sqlite3_step(stmt);
                IntPtr api = Marshal.ReadIntPtr(storage);
                if (api == IntPtr.Zero)
                {
                    throw new SnowballTokenizerException("The FTS5 API pointer was null (FTS5 is not available in this SQLite build).");
                }

                return api;
            }
            finally
            {
                sqlite3_finalize(stmt);
                Marshal.FreeHGlobal(storage);
            }
        }

        // ---------------------------------------------------------------------
        // Unmanaged callbacks (fts5_tokenizer)
        // ---------------------------------------------------------------------
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int XCreate(IntPtr pContext, IntPtr azArg, int nArg, IntPtr* ppOut)
        {
            try
            {
                RegistrationContext? context = pContext != IntPtr.Zero
                    ? GCHandle.FromIntPtr(pContext).Target as RegistrationContext
                    : null;

                SnowballTokenizerOptions options = ParseArguments(azArg, nArg, context?.Options ?? new SnowballTokenizerOptions());

                Stemmer? stemmer = null;
                if (options.StemmingEnabled && !IsNoStem(options.Language))
                {
                    stemmer = SnowballStemmers.Create(options.Language);
                }

                var instance = new TokenizerInstance(stemmer, options.RemoveDiacritics, options.MinTokenLength, options.MaxTokenLength);
                *ppOut = GCHandle.ToIntPtr(GCHandle.Alloc(instance));
                return SQLITE_OK;
            }
            catch (Exception ex)
            {
                _lastError = ex;
                return SQLITE_ERROR;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void XDelete(IntPtr pTokenizer)
        {
            try
            {
                if (pTokenizer != IntPtr.Zero)
                {
                    GCHandle.FromIntPtr(pTokenizer).Free();
                }
            }
            catch
            {
                // Never let an exception cross back into native code.
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int XTokenize(IntPtr pTokenizer, IntPtr pContext, int flags, IntPtr pText, int nText, IntPtr xToken)
        {
            try
            {
                if (pTokenizer == IntPtr.Zero)
                {
                    return SQLITE_OK;
                }

                var instance = (TokenizerInstance)GCHandle.FromIntPtr(pTokenizer).Target!;
                return instance.Tokenize(pContext, flags, pText, nText, xToken);
            }
            catch (Exception ex)
            {
                _lastError = ex;
                return SQLITE_ERROR;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void XDestroyContext(IntPtr pContext)
        {
            try
            {
                if (pContext == IntPtr.Zero)
                {
                    return;
                }

                GCHandle handle = GCHandle.FromIntPtr(pContext);
                if (handle.Target is RegistrationContext context && context.NamePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(context.NamePtr);
                }

                handle.Free();
            }
            catch
            {
                // Never let an exception cross back into native code.
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private static bool IsNoStem(string language) =>
            string.IsNullOrWhiteSpace(language)
            || language.Equals("none", StringComparison.OrdinalIgnoreCase)
            || language.Equals("raw", StringComparison.OrdinalIgnoreCase);

        private static SnowballTokenizerOptions ParseArguments(IntPtr azArg, int nArg, SnowballTokenizerOptions defaults)
        {
            var options = new SnowballTokenizerOptions
            {
                Language = defaults.Language,
                RemoveDiacritics = defaults.RemoveDiacritics,
                MinTokenLength = defaults.MinTokenLength,
                MaxTokenLength = defaults.MaxTokenLength,
                StemmingEnabled = defaults.StemmingEnabled,
            };

            if (azArg == IntPtr.Zero || nArg <= 0)
            {
                return options;
            }

            var args = new List<string>(nArg);
            for (int i = 0; i < nArg; i++)
            {
                IntPtr p = Marshal.ReadIntPtr(azArg, i * IntPtr.Size);
                args.Add(p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(p) ?? string.Empty);
            }

            int index = 0;
            if (args.Count > 0 && !IsKnownKey(args[0]))
            {
                options.Language = args[0];
                index = 1;
            }

            for (; index < args.Count; index++)
            {
                string key = args[index].ToLowerInvariant();
                string value = index + 1 < args.Count ? args[index + 1] : string.Empty;
                switch (key)
                {
                    case "language":
                        options.Language = value;
                        index++;
                        break;
                    case "remove_diacritics":
                        options.RemoveDiacritics = value is "1" or "2" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        index++;
                        break;
                    case "min_token_length":
                        if (int.TryParse(value, out int min))
                        {
                            options.MinTokenLength = min;
                        }

                        index++;
                        break;
                    case "max_token_length":
                        if (int.TryParse(value, out int max))
                        {
                            options.MaxTokenLength = max;
                        }

                        index++;
                        break;
                    case "stem":
                        options.StemmingEnabled = !(value is "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("off", StringComparison.OrdinalIgnoreCase));
                        index++;
                        break;
                    default:
                        // Unknown tokens are ignored so future options stay backward-compatible.
                        break;
                }
            }

            return options;
        }

        private static bool IsKnownKey(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "language":
                case "remove_diacritics":
                case "min_token_length":
                case "max_token_length":
                case "stem":
                    return true;
                default:
                    return false;
            }
        }

        private static IntPtr AllocPersistentUtf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }
    }
}
#endif
