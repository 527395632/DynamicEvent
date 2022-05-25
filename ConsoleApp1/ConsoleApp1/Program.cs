using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        public delegate void TestHandler();
        public delegate void TestHandler1(object obj);
        public delegate void TestHandler2(object obj, object obj1);
        public delegate void TestHandler3(int age);
        public delegate void TestHandler4(int age, int count);

        public event TestHandler OnTest;
        public event TestHandler1 OnTest1;
        public event TestHandler2 OnTest2;
        public event TestHandler3 OnTest3;
        public event TestHandler4 OnTest4;



        static void Main(string[] args)
        {
            var program = new Program();

            var list = new List<DynamicEvent>();

            foreach (var item in program.GetType().GetEvents())
            {
                var e = new DynamicEvent(program, item);
                e.OnInvoke += (s, aargs) =>
                {
                    Console.WriteLine();
                    Console.WriteLine(JsonConvert.SerializeObject(s));
                    Console.WriteLine(JsonConvert.SerializeObject(aargs));
                    Console.WriteLine("---------------------------");
                    return null;
                };
                e.Bind();
                list.Add(e);
            }

            program.OnTest();
            program.OnTest1(1);
            program.OnTest2(1, "123");
            program.OnTest3(2);
            program.OnTest4(3, 5);

            list.ForEach(item => item.UnBind());


            try
            {
                program.OnTest();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            try
            {
                program.OnTest1(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            try
            {
                program.OnTest2(1, "123");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            try
            {
                program.OnTest3(2);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            try
            {
                program.OnTest4(3, 5);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
    }


    public delegate object DynamicEventHandler(object sender, object[] args);
    public class DynamicEvent : IDisposable
    {
        private static List<DynamicEvent> _bindEvents = new List<DynamicEvent>();

        private IntPtr _handle = IntPtr.Zero;
        private Delegate _method;
        public event DynamicEventHandler OnInvoke;

        public DynamicEvent(object target, EventInfo e)
        {
            _handle = GCHandle.ToIntPtr(GCHandle.Alloc(this));
            Target = target;
            EveInfo = e;
            var types = EveInfo.EventHandlerType.GetMethod("Invoke").GetParameters().Select(q => q.ParameterType).ToArray();
            var method = new DynamicMethod(string.Empty, null, types, typeof(DynamicEvent).Module);
            var gen = method.GetILGenerator();
            if (IntPtr.Size == 8)
                gen.Emit(OpCodes.Ldc_I8, _handle.ToInt64());
            else
                gen.Emit(OpCodes.Ldc_I4, _handle.ToInt32());
            gen.Emit(OpCodes.Ldc_I4_S, types.Length);
            gen.Emit(OpCodes.Newarr, typeof(object));
            for (int i = 0; i < types.Length; i++)
            {
                gen.Emit(OpCodes.Dup);
                gen.Emit(OpCodes.Ldc_I4, i);
                gen.Emit(OpCodes.Ldarg, i);
                if (types[i].IsValueType)
                    gen.Emit(OpCodes.Box, types[i]);
                gen.Emit(OpCodes.Stelem_Ref);
            }
            gen.Emit(OpCodes.Call, typeof(DynamicEvent).GetMethod("OnEventExecute", BindingFlags.NonPublic | BindingFlags.Static));
            gen.Emit(OpCodes.Pop);
            gen.Emit(OpCodes.Ret);
            _method = method.CreateDelegate(EveInfo.EventHandlerType);
        }

        public object Target { get; }
        public EventInfo EveInfo { get; }
        private bool _isBind;

        public void Bind()
        {
            if (!_isBind)
            {
                lock (this)
                {
                    if (!_isBind)
                    {
                        EveInfo.AddEventHandler(Target, _method);
                        _bindEvents.Add(this);
                        _isBind = true;
                        return;
                    }
                }
            }
            throw new NotSupportedException("事件已绑定, 请解绑后再尝试!");
        }

        public void UnBind()
        {
            if (_isBind)
            {
                lock (this)
                {
                    if (_isBind)
                    {
                        EveInfo.RemoveEventHandler(Target, _method);
                        _bindEvents.Remove(this);
                        _isBind = false;
                    }
                }
            }
        }

        internal static object OnEventExecute(IntPtr self, object[] args)
        {
            var context = _bindEvents.FirstOrDefault(q => q._handle.Equals(self));
            if (context != null && context._isBind)
            {
                if (args.Length > 0 && args[0] == context.Target)
                {
                    var arr = new object[args.Length - 1];
                    Array.Copy(args, 1, arr, 0, arr.Length);
                    args = arr;
                }
                return context.OnInvoke(context.Target, args);
            }
            return null;
        }

        ~DynamicEvent() => Dispose();
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            try
            {
                UnBind();
            }
            catch
            { }
            try
            {
                GCHandle.FromIntPtr(_handle).Free();
            }
            catch
            { }
        }
    }
}
