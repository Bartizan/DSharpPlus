﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DSharpPlus.CommandsNext.Converters;

namespace DSharpPlus.CommandsNext
{
    /// <summary>
    /// Various CommandsNext-related utilities.
    /// </summary>
    public static class CommandsNextUtilities
    {
        private static Regex UserRegex { get; set; }
        private static Dictionary<Type, IArgumentConverter> ArgumentConverters { get; set; }
        private static MethodInfo ConvertGeneric { get; set; }
        private static Dictionary<Type, string> UserFriendlyTypeNames { get; set; }

        static CommandsNextUtilities()
        {
            UserRegex = new Regex(@"<@\!?(\d+?)> ");

            ArgumentConverters = new Dictionary<Type, IArgumentConverter>
            {
                [typeof(string)] = new StringConverter(),
                [typeof(bool)] = new BoolConverter(),
                [typeof(sbyte)] = new Int8Converter(),
                [typeof(byte)] = new Uint8Converter(),
                [typeof(short)] = new Int16Converter(),
                [typeof(ushort)] = new Uint16Converter(),
                [typeof(int)] = new Int32Converter(),
                [typeof(uint)] = new Uint32Converter(),
                [typeof(long)] = new Int64Converter(),
                [typeof(ulong)] = new Uint64Converter(),
                [typeof(float)] = new Float32Converter(),
                [typeof(double)] = new Float64Converter(),
                [typeof(decimal)] = new Float128Converter(),
                [typeof(DateTime)] = new DateTimeConverter(),
                [typeof(DateTimeOffset)] = new DateTimeOffsetConverter(),
                [typeof(TimeSpan)] = new TimeSpanConverter(),
                [typeof(DiscordUser)] = new DiscordUserConverter(),
                [typeof(DiscordMember)] = new DiscordMemberConverter(),
                [typeof(DiscordRole)] = new DiscordRoleConverter(),
                [typeof(DiscordChannel)] = new DiscordChannelConverter(),
                [typeof(DiscordGuild)] = new DiscordGuildConverter()
            };

            var t = typeof(CommandsNextUtilities);
            var ms = t.GetTypeInfo().DeclaredMethods;
            var m = ms.FirstOrDefault(xm => xm.Name == "ConvertArgument" && xm.ContainsGenericParameters && xm.IsStatic && xm.IsPublic);
            ConvertGeneric = m;

            UserFriendlyTypeNames = new Dictionary<Type, string>()
            {
                [typeof(string)] = "string",
                [typeof(bool)] = "boolean",
                [typeof(sbyte)] = "signed byte",
                [typeof(byte)] = "byte",
                [typeof(short)] = "short",
                [typeof(ushort)] = "unsigned short",
                [typeof(int)] = "int",
                [typeof(uint)] = "unsigned int",
                [typeof(long)] = "long",
                [typeof(ulong)] = "unsigned long",
                [typeof(float)] = "float",
                [typeof(double)] = "double",
                [typeof(decimal)] = "decimal",
                [typeof(DateTime)] = "date and time",
                [typeof(DateTimeOffset)] = "date and time",
                [typeof(TimeSpan)] = "time span",
                [typeof(DiscordUser)] = "user",
                [typeof(DiscordMember)] = "member",
                [typeof(DiscordRole)] = "role",
                [typeof(DiscordChannel)] = "channel",
                [typeof(DiscordGuild)] = "guild"
            };
        }

        /// <summary>
        /// Checks whether the message has a specified string prefix.
        /// </summary>
        /// <param name="msg">Message to check.</param>
        /// <param name="str">String to check for.</param>
        /// <returns>Positive number if the prefix is present, -1 otherwise.</returns>
        public static int HasStringPrefix(this DiscordMessage msg, string str)
        {
            var cnt = msg.Content;
            if (str.Length >= cnt.Length)
                return -1;

            if (cnt.StartsWith(str))
                return str.Length;

            return -1;
        }

        /// <summary>
        /// Checks whether the message contains a specified mention prefix.
        /// </summary>
        /// <param name="msg">Message to check.</param>
        /// <param name="str">User to check for.</param>
        /// <returns>Positive number if the prefix is present, -1 otherwise.</returns>
        public static int HasMentionPrefix(this DiscordMessage msg, DiscordUser user)
        {
            var cnt = msg.Content;
            if (!cnt.StartsWith("<@"))
                return -1;

            var cni = cnt.IndexOf('>');
            var cnp = cnt.Substring(0, cni);
            var m = UserRegex.Match(cnp);
            if (!m.Success)
                return -1;

            var uid = ulong.Parse(m.Groups[1].Value);
            if (user.ID != uid)
                return -1;

            return m.Value.Length;
        }

        /// <summary>
        /// Converts a string to specified type.
        /// </summary>
        /// <typeparam name="T">Type to convert to.</typeparam>
        /// <param name="value">Value to convert.</param>
        /// <param name="ctx">Context in which to convert to.</param>
        /// <returns>Converted object.</returns>
        public static object ConvertArgument<T>(this string value, CommandContext ctx, bool optional, object dflt)
        {
            var t = typeof(T);
            if (!ArgumentConverters.ContainsKey(t))
                throw new ArgumentException("There is no converter specified for given type.", nameof(T));

            var cv = ArgumentConverters[t] as IArgumentConverter<T>;
            if (cv == null)
                throw new ArgumentException("Invalid converter registered for this type.", nameof(T));

            if (!cv.TryConvert(value, ctx, out var result))
                if (!optional)
                    throw new ArgumentException("Could not convert specified value to given type.", nameof(value));
                else
                    return (T)dflt;

            return result;
        }

        /// <summary>
        /// Converts a string to specified type.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="ctx">Context in which to convert to.</param>
        /// <param name="type">Type to convert to.</param>
        /// <returns>Converted object.</returns>
        public static object ConvertArgument(this string value, CommandContext ctx, Type type, bool optional, object dflt)
        {
            var m = ConvertGeneric.MakeGenericMethod(type);
            return m.Invoke(null, new object[] { value, ctx, optional, dflt });
        }
        
        /// <summary>
        /// Registers an argument converter for specified type.
        /// </summary>
        /// <typeparam name="T">Type for which to register the converter.</typeparam>
        /// <param name="converter">Converter to register.</param>
        public static void RegisterConverter<T>(IArgumentConverter<T> converter)
        {
            if (converter == null)
                throw new ArgumentNullException("Converter cannot be null.", nameof(converter));

            ArgumentConverters[typeof(T)] = converter;
        }

        /// <summary>
        /// Unregisters an argument converter for specified type.
        /// </summary>
        /// <typeparam name="T">Type for which to unregister the converter.</typeparam>
        public static void UnregisterConverter<T>()
        {
            var t = typeof(T);
            if (ArgumentConverters.ContainsKey(t))
                ArgumentConverters.Remove(t);
        }

        /// <summary>
        /// Registers a user-friendly type name.
        /// </summary>
        /// <typeparam name="T">Type to register the name for.</typeparam>
        /// <param name="value">Name to register.</param>
        public static void RegisterUserFriendlyTypeName<T>(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException("Name cannot be null or empty.", nameof(value));

            UserFriendlyTypeNames[typeof(T)] = value;
        }

        internal static string ToUserFriendlyName(this Type t)
        {
            if (UserFriendlyTypeNames.ContainsKey(t))
                return UserFriendlyTypeNames[t];
            return t.Name;
        }

        /// <summary>
        /// Parses given argument string into individual strings.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <returns>Enumerator of parsed strings.</returns>
        public static IEnumerable<string> SplitArguments(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                yield break;

            var stra = str.Split(' ');
            var strt = "";
            foreach (var xs in stra)
            {
                if (strt == "")
                {
                    if (xs.StartsWith("\"") && xs.EndsWith("\""))
                    {
                        if (xs[xs.Length - 2] != '\\')
                            yield return xs.Substring(1, xs.Length - 2);
                        else
                            strt = xs.Substring(1).Remove(xs.Length - 3, 1);
                    }
                    else if (xs.StartsWith("\""))
                    {
                        strt = xs.Substring(1);
                    }
                    else
                    {
                        yield return xs;
                    }
                }
                else
                {
                    if (xs.EndsWith("\""))
                    {
                        if (xs[xs.Length - 2] != '\\')
                        {
                            strt = string.Concat(strt, " ", xs.Substring(0, xs.Length - 1));
                            yield return strt;
                            strt = "";
                        }
                        else
                        {
                            strt = string.Concat(strt, " ", xs.Remove(xs.Length - 3, 1));
                        }
                    }
                    else
                    {
                        strt = string.Concat(strt, " ", xs);
                    }
                }
            }
        }

        internal static object[] BindArguments(CommandContext ctx)
        {
            var cmd = ctx.Command;

            var args = new object[cmd.Arguments.Count + 1];
            if (ctx.RawArguments.Count < cmd.Arguments.Count(xa => !xa.IsOptional && !xa.IsCatchAll))
                throw new ArgumentException("Not enough arguments were supplied.");

            if (ctx.RawArguments.Count > cmd.Arguments.Count && ((cmd.Arguments.Any() && !cmd.Arguments.Last().IsCatchAll) || cmd.Arguments.Count == 0))
                throw new ArgumentException("Too many arguments were supplied.");

            args[0] = ctx;

            for (int i = 0; i < ctx.RawArguments.Count; i++)
                if (!cmd.Arguments[i].IsCatchAll)
                    args[i + 1] = ConvertArgument(ctx.RawArguments[i], ctx, cmd.Arguments[i].Type, cmd.Arguments[i].IsOptional, cmd.Arguments[i].DefaultValue);
                else
                {
                    args[i + 1] = Array.CreateInstance(cmd.Arguments[i].Type, ctx.RawArguments.Count - i);
                    var t = ctx.RawArguments.Skip(i).Select(xs => ConvertArgument(xs, ctx, cmd.Arguments[i].Type, false, null)).ToArray();
                    Array.Copy(t, (Array)args[i + 1], t.Length);
                    break;
                }

            if (ctx.RawArguments.Count < args.Length - 1)
                for (int i = ctx.RawArguments.Count; i < cmd.Arguments.Count; i++)
                    if (cmd.Arguments[i].IsOptional)
                        args[i + 1] = cmd.Arguments[i].DefaultValue;
                    else if (cmd.Arguments[i].IsCatchAll)
                        args[i + 1] = Array.CreateInstance(cmd.Arguments[i].Type, 0);

            return args;
        }
    }
}