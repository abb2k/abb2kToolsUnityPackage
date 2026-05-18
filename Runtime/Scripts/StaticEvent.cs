using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class ListenerHandle
{
    public int Priority {get; set;}
    System.Delegate callback;
    System.Action<ListenerHandle> OnErased = null;
    bool isLeak;
    public PresistListenerHandle PresistHandle {get; private set;}

    public ListenerHandle(System.Delegate callback, System.Action<ListenerHandle> onErased)
    {
        UnityEngine.Debug.Log("created l");
        this.callback = callback;
        this.OnErased = onErased;

        PresistHandle = new()
        {
            weakHandler = new(this)
        };
    }

    ~ListenerHandle()
    {
        UnityEngine.Debug.Log("erased l");
        if (isLeak) return;

        Destroy();
    }

    public void Leak()
    {
        isLeak = true;
        PresistHandle.handler = this;
        PresistHandle.weakHandler = null;
    }

    public void Destroy()
    {
        OnErased?.Invoke(this);
    }

    public void Invoke(object[] parameters) => callback.DynamicInvoke(parameters);

    public ListenerHandle BindTo(MonoBehaviour owner)
    {
        owner.destroyCancellationToken.Register(() => this.Destroy());
        return this;
    }
}

public class PresistListenerHandle
{
    public ListenerHandle handler;
    public System.WeakReference<ListenerHandle> weakHandler;
}

public abstract class StaticEventBase
{
    private static SortedDictionary<int, List<PresistListenerHandle>> instancesByPriority = new();

    protected void Send(object[] parameters)
    {
        foreach (var (_, refList) in instancesByPriority)
        {
            for (int i = refList.Count - 1; i >= 0; i--)
            {
                ListenerHandle handler = null;

                if (refList[i].weakHandler.TryGetTarget(out handler) || refList[i].handler != null)
                {
                    if (refList[i].handler != null)
                        handler = refList[i].handler;

                    handler.Invoke(parameters);
                }
                else if (refList[i].handler == null)
                {
                    refList.RemoveAt(i);
                }
            }
        }
    }

    protected ListenerHandle Listen(System.Delegate callback, int priority)
    {
        var listener = new ListenerHandle(callback, OnErased);
        listener.Priority = priority;

        if (!instancesByPriority.TryGetValue(priority, out var list))
        {
            list = new();
            instancesByPriority[priority] = list;
        }

        list.Add(listener.PresistHandle);

        return listener;
    }

    void OnErased(ListenerHandle handle)
    {
        if (!instancesByPriority.TryGetValue(handle.Priority, out var list)) return;

        list.RemoveAll(wr => wr.weakHandler.TryGetTarget(out var target) && ReferenceEquals(target, handle) || ReferenceEquals(wr.handler, handle));
    }
}



public class StaticEvent : StaticEventBase
{
    public void Send()
    {
        base.Send(new object[0]);
    }

    public ListenerHandle Listen(System.Action callback, int priority = 0)
    {
        return base.Listen(callback, priority);
    }
}



public class StaticEvent<T1> : StaticEventBase
{
    public void Send(T1 param1)
    {
        base.Send(new object[1]{param1});
    }

    public ListenerHandle Listen(System.Action<T1> callback, int priority = 0)
    {
        return base.Listen(callback, priority);
    }
}



public class StaticEvent<T1, T2> : StaticEventBase
{
    public void Send(T1 param1, T2 param2)
    {
        base.Send(new object[2]{param1, param2});
    }

    public ListenerHandle Listen(System.Action<T1, T2> callback, int priority = 0)
    {
        return base.Listen(callback, priority);
    }
}



public class StaticEvent<T1, T2, T3> : StaticEventBase
{
    public void Send(T1 param1, T2 param2, T3 param3)
    {
        base.Send(new object[3]{param1, param2, param3});
    }

    public ListenerHandle Listen(System.Action<T1, T2, T3> callback, int priority = 0)
    {
        return base.Listen(callback, priority);
    }
}



public class StaticEvent<T1, T2, T3, T4> : StaticEventBase
{
    public void Send(T1 param1, T2 param2, T3 param3, T4 param4)
    {
        base.Send(new object[4]{param1, param2, param3, param4});
    }

    public ListenerHandle Listen(System.Action<T1, T2, T3, T4> callback, int priority = 0)
    {
        return base.Listen(callback, priority);
    }
}