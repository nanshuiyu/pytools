﻿ # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 # copy of the license can be found in the License.html file at the root of this distribution. If 
 # you cannot locate the Apache License, Version 2.0, please send an email to 
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################

# To update the baseline DB:
#  1. Check out all files in Python\Product\PythonTools\CompletionDB
#  2. Run "ipy.exe RefreshDefaultDB.py" in Python\Product\PythonTools
#  3. Run a full analysis against CPython 2.7
#  4. Copy unittest.idb and unittest.case.idb to CompletionDB
#  5. Rerun "ipy.exe RefreshDefaultDB.py" in Python\Product\PythonTools
#  6. Undo unnecessary edits (tfpt uu)
#

import os
import sys
try:
    from cPickle import load, dump
except ImportError:
    from pickle import load, dump

# Add Analyzer to the search path so we can import PythonScraper
sys.path.append(os.path.join(os.path.split(os.path.split(os.path.abspath(__file__))[0])[0], "Analyzer"))
from PythonScraper import *

def replace_tuple_contents(type_name, orig, orig_name):
    return tuple(replace_list_contents(type_name, orig, orig_name))

def replace_list_contents(type_name, orig, orig_name):
    res = []
    for v in orig:
        if isinstance(v, tuple):
            res.append(replace_tuple_contents(type_name, v, orig_name))
        elif isinstance(v, list):
            res.append(replace_list_contents(type_name, v, orig_name))
        elif isinstance(v, dict):
            res.append(replace_dict_contents(type_name, v, orig_name))
        elif v == orig_name:
            res.append(type_name)
        else:
            res.append(v)
    return res

def replace_dict_contents(type_name, orig, orig_name):
    res = {}
    for k, v in orig.items():
        if isinstance(v, tuple):
            res[k] = replace_tuple_contents(type_name, v, orig_name)
        elif isinstance(v, list):
            res[k] = replace_list_contents(type_name, v, orig_name)
        elif isinstance(v, dict):
            res[k] = replace_dict_contents(type_name, v, orig_name)
        elif v == orig_name:
            res[k] = type_name
        else:
            res[k] = v
    return res

def main():
    #assert sys.platform == 'cli', "This script should be run with IronPython"
    outpath = os.path.join(os.path.split(os.path.abspath(__file__))[0], "CompletionDB")
    res = generate_builtin_module()
    res = module_fixers.get(builtin_name, lambda x: x)(res)

    orig = res['members']['str']
    orig_iter = res['members']['str_iterator']

    res['members']['bytes'] = replace_dict_contents('bytes', orig, 'str')
    res['members']['unicode'] = replace_dict_contents('unicode', orig, 'str')

    res['members']['bytes_iterator'] = replace_dict_contents('bytes_iterator', orig_iter, 'str_iterator')
    res['members']['unicode_iterator'] = replace_dict_contents('unicode_iterator', orig_iter, 'str_iterator')

    write_module(builtin_name, outpath, res)
    
    for mod_name in sys.builtin_module_names:
        if mod_name == builtin_name or mod_name == '__main__': continue
        if not os.path.exists(os.path.join(outpath, mod_name + '.idb')):
            print('Skipping ' + mod_name)
            continue
        
        res = generate_module(lookup_module(mod_name))
        if res is not None:
            res = module_fixers.get(mod_name, lambda x: x)(res)
            try:
                write_module(mod_name, outpath, res)
            except ValueError:
                pass

        if os.path.exists(os.path.join(outpath, mod_name + '.idb.$memlist')):
            os.unlink(os.path.join(outpath, mod_name + '.idb.$memlist'))

    # These modules should be obtained from a CPython 2.7 analysis. This removes
    # members that are unnecessary for the default DB.
    for mod_name in ('unittest', 'unittest.case'):
        if os.path.exists(os.path.join(outpath, mod_name + '.idb')):
            f = open(os.path.join(outpath, mod_name + '.idb'), 'rb')
            try:
                res = load(f)
            finally:
                f.close()

            res = module_fixers.get(mod_name, lambda x: x)(res)

            f = open(os.path.join(outpath, mod_name + '.idb'), 'wb')
            try:
                dump(res, f)
            finally:
                f.close()


##############################################################################
# Fixers for multi-targetting of different Python versions w/ the default 
# completion DB.  These add version tags based upon the data generated by 
# VersionDiff.py.  They also add members which are new in 3.x - those are
# all explicitly hard coded values.  The combination of this is that we get
# a default completion DB that can handle different versions of Python.  Then
# the default DB is used as a starting point for scanning the actual installed
# distribution and coming up w/ the real live completion members.

def mark_maximum_version(mod, items, version):
    for two_only in items:
        if two_only in mod['members']:
            # if we're running on a later version for the real scrape we may not have the member
            mod['members'][two_only]['version'] = '<=' + version

def mark_minimum_version(mod, items, version):
    for two_only in items:
        if two_only in mod['members']:
            # if we're running on a later version for the real scrape we may not have the member
            mod['members'][two_only]['version'] = '>=' + version

# InitMethodEntry, NewMethodEntry, and ReprMethodEntry are imported from
# PythonScraper.

def thread_fixer(mod):
    mark_minimum_version(mod, ['_count'], '2.7')

    # 3.x members
    mod['members']['TIMEOUT_MAX'] = {
        'kind': 'data',
        'version' : '>=3.2',
        'value': { 'type': type_to_typelist(float) }
    }
    mod['members']['RLock'] = {
        'kind': 'type',
        'version': '>=3.2',
        'value':  {
            'bases': type_to_typelist(object),
            'mro': [typename_to_typeref('_thread', 'RLock'), type_to_typeref(object)],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(NoneType)}
                },
                '__enter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'acquire(blocking=True) -> bool\n\n'
                               'Lock the lock.  `blocking` indicates '
                               'whether we should wait\nfor the lock '
                               'to be available or not.  If `blocking`'
                               ' is False\nand another thread holds the '
                               'lock, the method will return False\n'
                               'immediately.  If `blocking` is True and '
                               'another thread holds\nthe lock, the method '
                               'will wait for the lock to be released,\n'
                               'take it and then return True.\n(note: the '
                               'blocking operation is interruptible.)\n\nIn'
                               ' all other cases, the method will return '
                               'True immediately.\nPrecisely, if the '
                               'current thread already holds the lock, '
                               'its\ninternal counter is simply incremented.'
                               ' If nobody holds the lock,\nthe lock is '
                               'taken and its internal counter initialized '
                               'to 1.',
                        'overloads': None
                    }
                },
                '__exit__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'release()\n\nRelease the lock,'
                               ' allowing another thread that is blocked'
                               ' waiting for\nthe lock to acquire the lock.'
                               '  The lock must be in the locked state,\nand'
                               ' must be locked by the same thread that'
                               ' unlocks it; otherwise a\n`RuntimeError` is '
                               'raised.\n\nDo note that if the lock was '
                               'acquire()d several times in a row by the\n'
                               'current thread, release() needs to be called'
                               ' as many times for the lock\nto be available '
                               'for other threads.',
                        'overloads': None
                    }
                },
                '__new__': NewMethodEntry,
                '__repr__': ReprMethodEntry,
                '_acquire_restore': {
                    'kind': 'method',
                    'value': {
                        'doc': '_acquire_restore(state) -> None\n\n'
                               'For internal use by `threading.Condition`.',
                        'overloads': [generate_overload(NoneType, ('self', object), ('state', object))]
                    }
                },
                '_is_owned': {
                    'kind': 'method',
                    'value': {
                        'doc': '_is_owned() -> bool\n\nFor internal use by'
                               ' `threading.Condition`.',
                        'overloads': [generate_overload(bool, ('self', object))]
                    }
                },
                '_release_save': {
                    'kind': 'method',
                    'value': {
                        'doc': '_release_save() -> tuple\n\nFor internal use'
                               ' by `threading.Condition`.',
                        'overloads': [generate_overload(tuple, ('self', object))]
                    }
                },
                'acquire': {
                    'kind': 'method',
                    'value': {
                        'doc': 'acquire(blocking=True) -> bool\n\n'
                               'Lock the lock.  `blocking` indicates '
                               'whether we should wait\nfor the lock '
                               'to be available or not.  If '
                               '`blocking` is False\nand another thread'
                               ' holds the lock, the method will return '
                               'False\nimmediately.  If `blocking` is True'
                               ' and another thread holds\nthe lock, the '
                               'method will wait for the lock to be '
                               'released,\ntake it and then return True.\n'
                               '(note: the blocking operation is '
                               'interruptible.)\n\nIn all other cases, the'
                               ' method will return True immediately.\n'
                               'Precisely, if the current thread already holds'
                               ' the lock, its\ninternal counter is simply '
                               'incremented. If nobody holds the lock,\nthe'
                               ' lock is taken and its internal counter '
                               'initialized to 1.',
                        'overloads': None
                    }
                },
                'release': {
                    'kind': 'method',
                    'value': {
                        'doc': 'release()\n\nRelease the lock, '
                               'allowing another thread that is blocked'
                               ' waiting for\nthe lock to acquire the lock.'
                               '  The lock must be in the locked state,\nand'
                               ' must be locked by the same thread that '
                               'unlocks it; otherwise a\n`RuntimeError` is'
                               ' raised.\n\nDo note that if the lock was '
                               'acquire()d several times in a row by the\n'
                               'current thread, release() needs to be '
                               'called as many times for the lock\n'
                               'to be available for other threads.',
                       'overloads': [generate_overload(NoneType, ('self', object))]
                    }
                }
            },
        }
    }
    return mod

def builtin_fixer(mod):
    mark_maximum_version(mod, ['StandardError', 'apply', 'basestring', 
                               'buffer', 'cmp', 'coerce', 'execfile', 'file', 
                               'intern', 'long', 'raw_input', 'reduce', 
                               'reload', 'unichr', 'unicode', 'xrange'], '2.7')
    mark_minimum_version(mod, ['bytearray', 'bin', 'format', 'bytes', 
                               'BytesWarning', 'next', 'BufferError'], '2.6')
    mark_minimum_version(mod, ['memoryview'], '2.7')

    for iter_type in [
        'generator',
        'list_iterator',
        'tuple_iterator',
        'set_iterator',
        'str_iterator',
        'bytes_iterator',
        'unicode_iterator',
        'callable_iterator',
    ]:
        memb = mod['members'].get(iter_type)
        if not memb:
            continue
        if memb['kind'] == 'typeref':
            memb = mod['members'][memb['value'][0][1]]
        if '__next__' in memb['value']['members']:
            continue
        next2x = memb['value']['members']['next']
        next3x = dict(next2x)
        next2x['version'] = '<=2.7'
        next3x['version'] = '>=3.0'
        memb['value']['members']['__next__'] = next3x

    # new in 3x: exec, ascii, ResourceWarning, print

    mod['members']['exec'] = {
        'kind': 'function',
        'version': '>=3.0',
        'value': {
            'doc': 'exec(object[, globals[, locals]])\nRead and execute code from'
                   ' an object, which can be a string or a code\nobject.\nThe globals'
                   ' and locals are dictionaries, defaulting to the current\nglobals'
                   ' and locals.  If only globals is given, locals defaults to it.',
            'overloads': [generate_overload(
                object,
                ('object', object),
                ('globals', dict, '', 'None'),
                ('locals', dict, '', 'None')
            )],
        }
    }
    
    
    mod['members']['print'] = {
        'kind': 'function',
        'version': '>=3.0',
        'value': {
            'doc': 'print(value, *args, sep=\' \', end=\'\\n\', file=sys.stdout)\n\n'
            'Prints the values to a stream, or to sys.stdout by default.\nOptional'
            ' keyword arguments:\nfile: a file-like object (stream); defaults to '
            'the current sys.stdout.\nsep:  string inserted between values, '
            'default a space.\nend:  string appended after the last value, default '
            'a newline.',
            'overloads': [generate_overload(NoneType,
                                            ('value', object),
                                            ('sep', str, '', "' '"),
                                            ('file', typename_to_typeref('io', 'IOBase'), '', 'sys.stdout')
                                           )],
        }
        
    }

    # ResourceWarning, new in 3.2
    mod['members']['ResourceWarning'] = {
            'kind': 'type',
            'version': '>=3.2',
            'value': {
                'bases': [typename_to_typeref(builtin_name, 'Warning')],
                'doc': 'Base class for warnings about resource usage.',
                'members': {
                    '__doc__': { 'kind': 'data', 'value': { 'type': type_to_typelist(str) } },
                    '__init__': InitMethodEntry,
                    '__new__': NewMethodEntry,
                },
                'mro': [typename_to_typeref(builtin_name, 'ResourceWarning'),
                        typename_to_typeref(builtin_name, 'Warning'),
                        typename_to_typeref(builtin_name, 'Exception'),
                        typename_to_typeref(builtin_name, 'BaseException'),
                        typename_to_typeref(builtin_name, 'object')]
            }
        }

    return mod

def sys_fixer(mod):
    mark_maximum_version(mod, ['exc_clear', 'py3kwarning', 'maxint', 
                               'long_info', 'exc_type'], '2.7')
    mark_minimum_version(mod, ['dont_write_bytecode', 'float_info', 'gettrace',
                               'getprofile', 'py3kwarning', '__package__', 
                               'maxsize', 'flags','getsizeof', 
                               '_clear_type_cache'], '2.6')
    mark_minimum_version(mod, ['float_repr_style', 'long_info'], '2.7')

    # new in 3x

    mod['members']['int_info'] = {
        'kind': 'data', 
        'version': '>=3.1',
        'value': {
            'type': [typename_to_typeref('sys', 'int_info')], 
        }
    }
    mod['members']['_xoptions'] = {
        'kind': 'data', 
        'version': '>=3.1',
        'value': {
            'type': [typename_to_typeref(builtin_name, 'dict')], 
        }
    }
    mod['members']['intern'] = {
        'kind': 'function',
        'version': '>=3.0', 
        'value': {
            'doc': "intern(string) -> string\n\n"
                   "Intern the given string.  This enters the"
                   " string in the (global)\ntable of "
                   "interned strings whose purpose is to "
                   "speed up dictionary lookups.\nReturn"
                   " the string itself or the previously"
                   " interned string object with the\nsame "
                   "value.",
            'overloads': [generate_overload(str, ('string', str))]
        }
    }
    mod['members']['setswitchinterval'] = {
        'kind': 'function',
        'version': '>=3.2', 
        'value': {
            'doc': 'setswitchinterval(n)\n\n'
                   'Set the ideal thread switching '
                   'delay inside the Python '
                   'interpreter\nThe actual frequency'
                   ' of switching threads can be '
                   'lower if the\ninterpreter '
                   'executes long sequences of'
                   ' uninterruptible code\n(this is'
                   ' implementation-specific and '
                   'workload-dependent).\n\nThe '
                   'parameter must represent the '
                   'desired switching delay in '
                   'seconds\nA typical value '
                   'is 0.005 (5 milliseconds).',
            'overloads': [generate_overload(NoneType, ('n', float))]
        }
    }
    mod['members']['getswitchinterval'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'getswitchinterval() -> current'
                   'thread switch interval; see setswitchinterval().',
            'overloads': [generate_overload(float)]
        }
    }
    mod['members']['hash_info'] = {
        'kind': 'data',
        'version': '>=3.2', 
        'value': {
            'type': [typename_to_typeref('sys', 'hash_info')]
        }
    }
    return mod

def nt_fixer(mod):
    mark_maximum_version(mod, ['tempnam', 'tmpfile', 'fdopen', 'getcwd', 
                               'popen4', 'popen2', 'popen3', 'popen', 
                               'tmpnam'], '2.7')
    mark_minimum_version(mod, ['closerange'], '2.6')

    mod['members']['_isdir'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'Return true if the pathname refers to an existing'
                   ' directory.',
            'overloads': [generate_overload(bool, ('pathname', str))],
        }
    }

    mod['members']['getlogin'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'getlogin() -> string\n\nReturn the actual login name.',
            'overloads': [generate_overload(typename_to_typeref(builtin_name, 'code'))],
        }
    }
    mod['members']['_getfileinformation'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'overloads': None, 
        }
    }
    mod['members']['getppid'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': "getppid() -> ppid\n\nReturn the parent's process id.  "
                   "If the parent process has already exited,\nWindows "
                   "machines will still return its id; others systems will"
                   " return the id\nof the 'init' process (1).",
            'overloads': [generate_overload(int)],
        }
    }
    mod['members']['symlink'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'symlink(src, dst, target_is_directory=False)\n\nCreate'
                   ' a symbolic link pointing to src named dst.\n'
                   'target_is_directory is required if the target is to '
                   'be interpreted as\na directory.\nThis function requires'
                   ' Windows 6.0 or greater, and raises a\n'
                   'NotImplementedError otherwise.',
            'overloads': [generate_overload(NoneType, ('src', object), ('target_is_directory', bool, '', 'False'))],
        }
    }
    mod['members']['link'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'link(src, dst)\n\nCreate a hard link to a file.',
            'overloads': [generate_overload(NoneType, ('src', object), ('dst', object))],
        }
    }
    mod['members']['device_encoding'] = {
        'kind': 'function',
        'version': '>=3.0',
        'value': {
            'doc': 'device_encoding(fd) -> str\n\nReturn a string '
                   'describing the encoding of the device\nif the'
                   ' output is a terminal; else return None.',
            'overloads': [generate_overload(str, ('fd', object))],
        }
    }
    mod['members']['_getfinalpathname'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'overloads': None, 
        }
    }
    mod['members']['readlink'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'readlink(path) -> path\n\nReturn a string representing'
                   ' the path to which the symbolic link points.',
            'overloads': [generate_overload(str, ('path', object))],
        }
    }
    mod['members']['getcwdb'] = {
        'kind': 'function',
        'version': '>=3.0',
        'value': {
            'doc': 'getcwdb() -> path\n\nReturn a bytes string '
                   'representing the current working directory.',
            'overloads': [generate_overload(str)],
        }
    }
    return mod

def msvcrt_fixer(mod):
    mark_minimum_version(mod, ['LIBRARIES_ASSEMBLY_NAME_PREFIX', 
                               'CRT_ASSEMBLY_VERSION', 'getwche', 'ungetwch', 
                               'getwch', 'putwch', 
                               'VC_ASSEMBLY_PUBLICKEYTOKEN'], '2.6')

    mod['members']['SEM_FAILCRITICALERRORS'] = {
        'kind': 'data',
        'version': '>=3.2',
        'value': {'type': type_to_typelist(int)}
    }
    mod['members']['SEM_NOALIGNMENTFAULTEXCEPT'] = {
        'kind': 'data',
        'version': '>=3.2',
        'value': {'type': type_to_typelist(int)}
    }
    mod['members']['SetErrorMode'] = {
        'kind': 'function', 
        'version': '>=3.2',
        'value': {'overloads': None}
    }
    mod['members']['SEM_NOGPFAULTERRORBOX'] = {
        'kind': 'data',
        'version': '>=3.2',
        'value': {'type': type_to_typelist(int)}
    }
    mod['members']['SEM_NOOPENFILEERRORBOX'] = {
        'kind': 'data',
        'version': '>=3.2',
        'value': {'type': type_to_typelist(int)}
    }
    return mod

def gc_fixer(mod):
    mark_maximum_version(mod, ['DEBUG_OBJECTS', 'DEBUG_INSTANCES'], '2.7')
    mark_minimum_version(mod, ['is_tracked'], '2.7')
    return mod

def cmath_fixer(mod):
    mark_minimum_version(mod, ['polar', 'isnan', 'isinf', 'phase', 'rect'], 
                         '2.6')
    mark_minimum_version(mod, ['lgamma', 'expm1', 'erfc', 'erf', 'gamma'], 
                         '2.7')

    mod['members']['isfinite'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'isfinite(z) -> bool\nReturn True if both the real and '
                   'imaginary parts of z are finite, else False.',
            'overloads': [generate_overload(bool, ('z', object))],
        }
    }
    return mod

def _symtable_fixer(mod):
    mark_maximum_version(mod, ['OPT_BARE_EXEC', 'OPT_EXEC'], '2.7')
    mark_minimum_version(mod, ['SCOPE_OFF', 'SCOPE_MASK'], '2.6')

    mod['members']['OPT_TOPLEVEL'] = {
        'kind': 'data',
        'version': '>=3.2',
        'value': {'type': type_to_typelist(int)}
    }

    return mod

def _warnings_fixer(mod):
    mark_maximum_version(mod, ['default_action', 'once_registry'], '2.7')

    mod['members']['_defaultaction'] = {
        'kind': 'data',
        'version': '>=3.0',
        'value': {'type': type_to_typelist(str)}
    }
    mod['members']['_onceregistry'] = {
        'kind': 'data',
        'version': '>=3.0',
        'value': {'type': type_to_typelist(dict)}
    }

    return mod

def _codecs_fixer(mod):
    mark_maximum_version(mod, ['charbuffer_encode'], '2.7')
    mark_minimum_version(mod, ['utf_32_le_encode', 'utf_32_le_decode', 
                               'utf_32_be_decode', 'utf_32_be_encode', 
                               'utf_32_encode', 'utf_32_ex_decode', 
                               'utf_32_decode'], '2.6')
    return mod

def _md5_fixer(mod):
    mark_maximum_version(mod, ['new', 'MD5Type', 'digest_size'], '2.7')
    mod['members']['md5'] = {
        'kind': 'function',
        'version': '>=3.0',
        'value': {
            'doc': 'Return a new MD5 hash object; optionally initialized '
                   'with a string.',
            'overloads': None,
        }
    }

    return mod

def math_fixer(mod):
    mark_minimum_version(mod, ['isnan', 'atanh', 'factorial', 'fsum', 
                               'copysign', 'asinh', 'isinf', 'acosh', 
                               'log1p', 'trunc'], '2.6')

    mod['members']['isfinite'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'isfinite(x) -> bool\n\nReturn True if x is neither'
                   ' an infinity nor a NaN, and False otherwise.',
            'overloads': [generate_overload(bool, ('x', object))],
        }
    }

    return mod

def imp_fixer(mod):
    mark_minimum_version(mod, ['reload'], '2.6')

    mod['members']['source_from_cache'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'Given the path to a .pyc./.pyo file, return the path '
                   'to its .py file.\n\nThe .pyc/.pyo file does not need'
                   ' to exist; this simply returns the path to\nthe .py '
                   'file calculated to correspond to the .pyc/.pyo file.'
                   '  If path\ndoes not conform to PEP 3147 format, '
                   'ValueError will be raised.',
            'overloads': None,
        }
    }
    mod['members']['get_tag'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'get_tag() -> string\nReturn the magic tag for .pyc or'
                   ' .pyo files.',
            'overloads': [generate_overload(typename_to_typeref(builtin_name, 'code'))],
        }
    }
    mod['members']['cache_from_source'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'Given the path to a .py file, return the path to its'
                   ' .pyc/.pyo file.\n\nThe .py file does not need to '
                   'exist; this simply returns the path to the\n.pyc/'
                   '.pyo file calculated as if the .py file were '
                   'imported.  The extension\nwill be .pyc unless '
                   '__debug__ is not defined, then it will be .pyo.'
                   '\n\nIf debug_override is not None, then it must'
                   ' be a boolean and is taken as\nthe value of '
                   '__debug__ instead.',
            'overloads': None,
        }
    }
    mod['members']['is_frozen_package'] = {
        'kind': 'function', 
        'version': '>=3.1',
        'value': {
            'overloads': None, 
        }
    }
    return mod

def operator_fixer(mod):
    mark_maximum_version(mod, ['__idiv__', 'delslice', 'repeat', 
                               '__getslice__', '__setslice__', 'getslice', 
                               '__repeat__', '__delslice__', 'idiv', 
                               'isMappingType', 'isSequenceType', '__div__', 
                               '__irepeat__', 'setslice', 'irepeat', 
                               'isNumberType', 'isCallable', 
                               'sequenceIncludes', 'div'], '2.7')

    mark_minimum_version(mod, ['methodcaller'], '2.6')
    return mod

def itertools_fixer(mod):
    mark_maximum_version(mod, ['ifilter', 'izip', 'ifilterfalse', 
                               'imap', 'izip_longest'], '2.7')
    mark_minimum_version(mod, ['combinations', 'product', 'permutations', 
                               'izip_longest'], '2.6')
    mark_minimum_version(mod, ['combinations_with_replacement', 'compress'], 
                                '2.7')

    mod['members']['accumulate'] = {
        'kind': 'type',
        'version': '>=3.2',
        'value': {
            'bases': [type_to_typeref(object)],
            'mro': [typename_to_typeref('itertools', 'accumulate'), type_to_typeref(object)],
            'doc': 'accumulate(iterable) --> accumulate object\n\nReturn series of accumulated sums.',
            'members': {
                '__doc__': {'kind': 'data', 'value': { 'type': type_to_typelist(str) } },
                '__getattribute__': {
                    'kind': 'method',
                    'value': {
                        'doc': "x.__getattribute__('name') <==> x.name",
                        'overloads': None
                    }
                },
                '__iter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__iter__() <==> iter(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                },
                '__new__': {
                    'kind': 'function',
                    'value': {
                        'doc': 'T.__new__(S, ...) -> a new object '
                               'with type S, a subtype of T',
                        'overloads': [generate_overload(NoneType, ('iterable', object))],
                    }
                },
                '__next__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__next__() <==> next(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                },
            },
        },
    }
    mod['members']['zip_longest'] = {
        'kind': 'type',
        'version': '>=3.0',
        'value': {
            'bases': [type_to_typeref(object)],
            'mro': [typename_to_typeref('itertools', 'zip_longest'), type_to_typeref(object)],
            'doc': 'zip_longest(iter1 [,iter2 [...]], [fillvalue=None]) '
                   '--> zip_longest object\n\nReturn an zip_longest object whose'
                   ' .__next__() method returns a tuple where\nthe i-th element'
                   ' comes from the i-th iterable argument.  The .__next__()'
                   '\nmethod continues until the longest iterable in the argument'
                   ' sequence\nis exhausted and then it raises StopIteration.  '
                   'When the shorter iterables\nare exhausted, the fillvalue is '
                   'substituted in their place.  The fillvalue\ndefaults to None'
                   ' or can be specified by a keyword argument.\n',
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': { 'type': type_to_typelist(str) },
                },
                '__getattribute__': {
                    'kind': 'method',
                    'value': {
                        'doc': "x.__getattribute__('name') <==> x.name",
                        'overloads': None
                    }
                },
                '__iter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__iter__() <==> iter(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                },
                '__new__': NewMethodEntry,
                '__next__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__next__() <==> next(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                }
            },
        }
    }
    mod['members']['filterfalse'] = {
        'kind': 'type',
        'version': '>=3.0',
        'value': {
            'bases': [type_to_typeref(object)],
            'mro': [typename_to_typeref('itertools', 'filterfalse'), type_to_typeref(object)],
            'doc': 'filterfalse(function or None, sequence) --> '
                   'filterfalse object\n\nReturn those items of sequence'
                   ' for which function(item) is false.\nIf function is None,'
                   ' return the items that are false.',
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__getattribute__': {
                    'kind': 'method',
                    'value': {
                        'doc': "x.__getattribute__('name') <==> x.name",
                        'overloads': None
                    }
                },
                '__iter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__iter__() <==> iter(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    },
                },
                '__new__': NewMethodEntry,
                '__next__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__next__() <==> next(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                }
            },
        }
    }
    return mod

def cPickle_fixer(mod):
    mark_maximum_version(mod, ['HIGHEST_PROTOCOL', 'format_version', 
                               'UnpickleableError', '__builtin__', 
                               'BadPickleGet', '__version__', 
                               'compatible_formats'], '2.7')
    return mod

def _struct_fixer(mod):
    mark_maximum_version(mod, ['_PY_STRUCT_FLOAT_COERCE', 
                               '_PY_STRUCT_RANGE_CHECKING', '__version__'], '2.7')
    mark_minimum_version(mod, ['_clearcache', 'pack_into', 'calcsize', 
                               'unpack', 'unpack_from', 'pack'], '2.6')
    return mod

def parser_fixer(mod):
    mark_maximum_version(mod, ['compileast', 'ast2list', 'ASTType', 
                               'ast2tuple', 'sequence2ast', 'tuple2ast'], '2.7')
    return mod

def array_fixer(mod):
    mod['members']['_array_reconstructor'] = {
        'kind': 'function',
        'version': '>=3.2',
        'value': {
            'doc': 'Internal. Used for pickling support.',
            'overloads': None,
        }
    }
    mod['members']['typecodes'] = {
        'kind': 'data', 
        'version': '>=3.0',
        'value': {
            'type': type_to_typelist(str), 
        }
    }

    return mod

def _ast_fixer(mod):
    mark_maximum_version(mod, ['Print', 'Repr', 'Exec'], '2.7')
    mark_minimum_version(mod, ['ExceptHandler'], '2.6')
    mark_minimum_version(mod, ['SetComp', 'Set', 'DictComp'], '2.7')

    mod['members']['Starred'] = {
        'kind': 'type',
        'version': '>=3.0',
        'value': {
            'bases': [typename_to_typeref('_ast', 'expr')],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(NoneType)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                },
            },
            'mro': [typename_to_typeref('_ast', 'Starred'),
                    typename_to_typeref('_ast', 'expr'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
        }
    }
    mod['members']['Bytes'] = {
        'kind': 'type',
        'version': '>=3.0',
        'value': {
            'bases': [typename_to_typeref('_ast', 'expr')],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(object)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                }
            },
            'mro': [typename_to_typeref('_ast', 'Bytes'),
                    typename_to_typeref('_ast', 'expr'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
        }
    }
    mod['members']['Nonlocal'] = {
        'kind': 'type',
        'version': '>=3.0',
        'value': {
            'bases': [typename_to_typeref('_ast', 'stmt')],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(NoneType)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                }
            },
            'mro': [typename_to_typeref('_ast', 'Nonlocal'),
                    typename_to_typeref('_ast', 'stmt'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
        }
    }
    mod['members']['arg'] = {
        'kind': 'type',
        'version': '>=3.0',
        'value': {
            'bases': [typename_to_typeref('_ast', 'AST')],
            'members': {
                '__dict__': {
                    'kind': 'property',
                    'value': {
                        'doc': 'dictionary for instance variables (if defined)',
                        'type': type_to_typelist(object)
                    }
                },
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(object)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '__weakref__': {
                    'kind': 'property',
                    'value': {
                        'doc': 'list of weak references to the object (if defined)',
                        'type': type_to_typelist(object)
                    }
                },
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                }
            },
            'mro': [typename_to_typeref('_ast', 'arg'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
        }
    }
    return mod

def mmap_fixer(mod):
    mark_minimum_version(mod, ['ALLOCATIONGRANULARITY'], '2.6')
    return mod

def _functools_fixer(mod):
    mark_minimum_version(mod, ['reduce'], '2.6')
    return mod

def winreg_fixer(mod):
    mark_minimum_version(mod, ['QueryReflectionKey', 'DisableReflectionKey', 
                               '__package__', 'KEY_WOW64_32KEY', 
                               'KEY_WOW64_64KEY', 'EnableReflectionKey', 
                               'ExpandEnvironmentStrings'], '2.6')
    mark_minimum_version(mod, ['CreateKeyEx', 'DeleteKeyEx'], '2.7')
    return mod

def _heapq_fixer(mod):
    mark_minimum_version(mod, ['heapqpushpop'], '2.6')
    return mod

def exceptions_fixer(mod):
    mark_minimum_version(mod, ['BytesWarning', 'BufferError'], '2.6')
    return mod

def signal_fixer(mod):
    mark_minimum_version(mod, ['set_wakeup_fd'], '2.6')
    mark_minimum_version(mod, ['CTRL_C_EVENT', 'CTRL_BREAK_EVENT'], '2.7')
    return mod

def _subprocess_fixer(mod):
    mark_minimum_version(mod, ['CREATE_NEW_PROCESS_GROUP'], '2.7')
    return mod

def _json_fixer(mod):
    mark_minimum_version(mod, ['make_scanner', 'make_encoder'], '2.7')
    return mod


def unittest_fixer(mod):
    if 'filename' in mod:
        del mod['filename']

    children = mod['children']
    for name in [n for n in children if n != 'case']:
        children.remove(name)
    members = mod['members']
    for name in [n for n in members if n != 'TestCase' and n != 'case']:
        del members[name]

    return mod

def unittest_case_fixer(mod):
    if 'filename' in mod:
        del mod['filename']

    members = mod['members']
    for name in [n for n in members if n != 'TestCase']:
        del members[name]

    return mod

module_fixers = {
    'thread' : thread_fixer,
    '__builtin__' : builtin_fixer,
    'sys': sys_fixer,
    'nt': nt_fixer,
    'msvcrt': msvcrt_fixer,
    'gc' : gc_fixer,
    '_symtable': _symtable_fixer,
    '_warnings': _warnings_fixer,
    '_codecs': _codecs_fixer,
    '_md5' : _md5_fixer,
    'cmath' : cmath_fixer,
    'math': math_fixer,
    'imp': imp_fixer,
    'operator': operator_fixer,
    'itertools': itertools_fixer,
    'cPickle': cPickle_fixer,
    '_struct': _struct_fixer,
    'parser': parser_fixer,
    'array': array_fixer,
    '_ast': _ast_fixer,
    'mmap': mmap_fixer,
    '_functools': _functools_fixer,
    '_winreg': winreg_fixer,
    'exceptions': exceptions_fixer,
    'signal' : signal_fixer,
    '_subprocess' : _subprocess_fixer,
    '_json' : _json_fixer,
    'unittest' : unittest_fixer,
    'unittest.case' : unittest_case_fixer,
}


if __name__ == '__main__':
    main()

