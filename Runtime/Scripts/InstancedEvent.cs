using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abb2kTools
{
    [Serializable]
    internal sealed class InstancedEventPersistenceEntry
    {
        [SerializeField] public string EventTypeName;
        [SerializeField] public List<InstancedEventListenerPersistedState> Listeners = new();
    }

    [Serializable]
    internal sealed class InstancedEventListenerPersistedState
    {
        [SerializeField] public string UniqueId;
        [SerializeField] public string DelegateTypeName;
        [SerializeField] public string DeclaringTypeName;
        [SerializeField] public string MethodName;
        [SerializeField] public string TargetTypeName;
        [SerializeField] public int TargetInstanceId;
        [SerializeField] public int Priority;
        [SerializeField] public bool IsEnabled;
    }

    internal static class InstancedEventPersistence
    {
#if UNITY_EDITOR
        private const string PrefKeyPrefix = "Abb2kTools.InstancedEventPersistence.";
        private static readonly Dictionary<string, InstancedEventPersistenceEntry> cache = LoadCacheFromEditorPrefs();

        public static void SaveEventState(Type eventType, IEnumerable<InstancedEventListenerPersistedState> listeners)
        {
            if (eventType == null) return;

            var key = eventType.FullName ?? eventType.Name;
            var entry = new InstancedEventPersistenceEntry
            {
                EventTypeName = key,
                Listeners = new List<InstancedEventListenerPersistedState>(listeners ?? Array.Empty<InstancedEventListenerPersistedState>())
            };

            var json = JsonUtility.ToJson(entry);
            EditorPrefs.SetString(GetPrefKey(key), json);
            UpdatePrefIndex(GetPrefKey(key));
            cache[key] = entry;
        }

        public static InstancedEventPersistenceEntry LoadEventState(Type eventType)
        {
            if (eventType == null) return null;

            var key = eventType.FullName ?? eventType.Name;
            EnsureCacheLoaded();
            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var json = EditorPrefs.GetString(GetPrefKey(key), string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var entry = JsonUtility.FromJson<InstancedEventPersistenceEntry>(json);
            if (entry != null)
            {
                cache[key] = entry;
            }

            return entry;
        }

        public static void ClearEventState(Type eventType)
        {
            if (eventType == null) return;

            var key = eventType.FullName ?? eventType.Name;
            if (cache.ContainsKey(key))
            {
                cache.Remove(key);
            }

            EditorPrefs.DeleteKey(GetPrefKey(key));
            UpdatePrefIndex(GetPrefKey(key), remove: true);
        }

        private static string GetPrefKey(string eventTypeName)
        {
            return PrefKeyPrefix + eventTypeName;
        }

        private static void UpdatePrefIndex(string prefKey, bool remove = false)
        {
            var indexKey = "Abb2kTools.InstancedEventPersistence.Index"; 
            var current = PlayerPrefs.GetString(indexKey, string.Empty);
            var entries = new List<string>(current.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

            if (remove)
            {
                entries.RemoveAll(entry => string.Equals(entry, prefKey, StringComparison.Ordinal));
            }
            else if (!entries.Exists(entry => string.Equals(entry, prefKey, StringComparison.Ordinal)))
            {
                entries.Add(prefKey);
            }

            PlayerPrefs.SetString(indexKey, string.Join("|", entries));
            PlayerPrefs.Save();
        }

        private static Dictionary<string, InstancedEventPersistenceEntry> LoadCacheFromEditorPrefs()
        {
            var loaded = new Dictionary<string, InstancedEventPersistenceEntry>(StringComparer.Ordinal);

            var allPrefs = new List<string>();
            foreach (var prefKey in PlayerPrefs.GetString("Abb2kTools.InstancedEventPersistence.Index", string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                allPrefs.Add(prefKey);
            }

            foreach (var key in allPrefs)
            {
                if (!key.StartsWith(PrefKeyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var json = EditorPrefs.GetString(key, string.Empty);
                if (string.IsNullOrEmpty(json))
                {
                    continue;
                }

                var entry = JsonUtility.FromJson<InstancedEventPersistenceEntry>(json);
                if (entry == null)
                {
                    continue;
                }

                var eventTypeName = entry.EventTypeName;
                if (string.IsNullOrEmpty(eventTypeName))
                {
                    eventTypeName = key.Substring(PrefKeyPrefix.Length);
                }

                loaded[eventTypeName] = entry;
            }

            return loaded;
        }

        private static void EnsureCacheLoaded()
        {
            if (cache.Count != 0)
            {
                return;
            }

            var reloaded = LoadCacheFromEditorPrefs();
            foreach (var pair in reloaded)
            {
                cache[pair.Key] = pair.Value;
            }
        }
#else
        public static void SaveEventState(Type eventType, IEnumerable<InstancedEventListenerPersistedState> listeners) { }

        public static InstancedEventPersistenceEntry LoadEventState(Type eventType) => null;

        public static void ClearEventState(Type eventType) { }
#endif
    }

    public enum ListenerResult
    {
        Propagate,
        Block
    }

    public static class InstancedEventHandler
    {
        private static Dictionary<System.Type, InstancedEventBaseOpaque> events = new();

        internal static bool IsSpawning = false;
#if UNITY_EDITOR
        private const string EventRegistryPrefKey = "Abb2kTools.InstancedEventHandler.EventRegistry";
        private static bool eventRegistryLoaded;
        private static readonly HashSet<string> registeredEventTypes = new(StringComparer.Ordinal);
#endif

#if UNITY_EDITOR
        private static bool editorCallbacksRegistered;

        static InstancedEventHandler()
        {
            if (!editorCallbacksRegistered)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                editorCallbacksRegistered = true;
            }

            RestoreRegisteredEventsFromPersistence(); 
        }

        [InitializeOnLoadMethod]
        private static void InitializeFromPersistence()
        {
            EditorApplication.delayCall += RestoreRegisteredEventsFromPersistence;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SaveCurrentEventStateToPersistence();
                    RestoreCurrentEventStateFromPersistence();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    RestoreCurrentEventStateFromPersistence();
                    break;
            }
        }
#endif

        internal static T GetSharedEventInstance<T>() where T : InstancedEventBaseOpaque
        {
#if UNITY_EDITOR
            EnsureEventRegistryLoaded();
#endif

            if (!events.TryGetValue(typeof(T), out var existing))
            {
#if UNITY_EDITOR
                if (TryRestoreRegisteredEvent(typeof(T), out var restoredEvent))
                {
                    events[typeof(T)] = restoredEvent;
                    return (T)restoredEvent;
                }
#endif

                IsSpawning = true;
                T newEvent = System.Activator.CreateInstance<T>();
                IsSpawning = false;
                newEvent.RestoreFromPersistence();
                events.Add(typeof(T), newEvent);
#if UNITY_EDITOR
                RegisterEventType(typeof(T));
#endif
                return newEvent;
            }

            return (T)existing;
        }

        internal static void SaveCurrentEventStateToPersistence()
        {
            foreach (var pair in events)
            {
                pair.Value?.SaveToPersistence();
#if UNITY_EDITOR
                RegisterEventType(pair.Key);
#endif
            }
        }

        internal static void RestoreCurrentEventStateFromPersistence()
        {
            foreach (var pair in events)
            {
                pair.Value?.RestoreFromPersistence();
            }
        }

        internal static void RemoveListenerFromAllEvents(ListenerHandle handle)
        {
            if (handle == null) return;  
 
            foreach (var pair in events)
            {
                pair.Value?.RemoveMatchingListener(handle); 
            }
        }

#if UNITY_EDITOR
        private static void EnsureEventRegistryLoaded()
        {
            if (eventRegistryLoaded)
            {
                return;
            }

            registeredEventTypes.Clear();
            var serialized = EditorPrefs.GetString(EventRegistryPrefKey, string.Empty);
            if (string.IsNullOrEmpty(serialized))
            {
                eventRegistryLoaded = true;
                return;
            }

            foreach (var entry in serialized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrEmpty(entry))
                {
                    registeredEventTypes.Add(entry);
                }
            }

            eventRegistryLoaded = true;
        }

        private static void RegisterEventType(Type eventType)
        {
            if (eventType == null)
            {
                return;
            }

            EnsureEventRegistryLoaded();
            var typeName = eventType.FullName ?? eventType.Name;
            if (registeredEventTypes.Contains(typeName))
            {
                return;
            }

            registeredEventTypes.Add(typeName);
            SaveEventRegistry();
        }

        private static void RestoreRegisteredEventsFromPersistence()
        {
            EnsureEventRegistryLoaded();
            if (events.Count != 0)
            {
                return;
            }

            foreach (var typeName in registeredEventTypes)
            {
                var eventType = ResolveEventType(typeName);
                if (eventType == null || events.ContainsKey(eventType))
                {
                    continue;
                }

                if (TryRestoreRegisteredEvent(eventType, out var restoredEvent))
                {
                    events[eventType] = restoredEvent;
                }
            }
        }

        private static bool TryRestoreRegisteredEvent(Type eventType, out InstancedEventBaseOpaque restoredEvent)
        {
            restoredEvent = null;
            if (eventType == null)
            {
                return false;
            }

            EnsureEventRegistryLoaded();
            var typeName = eventType.FullName ?? eventType.Name;
            if (!registeredEventTypes.Contains(typeName))
            {
                return false;
            }

            IsSpawning = true;
            restoredEvent = (InstancedEventBaseOpaque)Activator.CreateInstance(eventType);
            IsSpawning = false;
            restoredEvent.RestoreFromPersistence();
            return true;
        }

        private static void SaveEventRegistry()
        {
            EnsureEventRegistryLoaded();
            var entries = new List<string>(registeredEventTypes);
            entries.Sort(StringComparer.Ordinal);
            EditorPrefs.SetString(EventRegistryPrefKey, string.Join("|", entries));
        }

        private static Type ResolveEventType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
#endif
    }
 
    [System.Serializable]
    public class ListenerHandle
    {
        [SerializeField]
        public int Priority;
        [SerializeField]
        public System.Delegate Callback;
        
        [SerializeField]
        public bool IsPlayModeListener;
        [System.NonSerialized]
        private InstancedEventBaseOpaque owner;
        
        [SerializeField] internal string uniqueId;
        
        [SerializeField] private string persistedDelegateTypeName;
        [SerializeField] private string persistedDeclaringTypeName;
        [SerializeField] private string persistedMethodName;
        [SerializeField] private string persistedTargetTypeName;
        [SerializeField] private int persistedTargetInstanceId;
        
        [SerializeField]
        internal UnityEvent onRestored = new();

        [SerializeField]
        internal bool isEnabled = true;
        [SerializeField]
        internal bool activeInEditor = false;

        public ListenerHandle(System.Delegate callback, InstancedEventBaseOpaque owner)
        {
            this.uniqueId = System.Guid.NewGuid().ToString();
            this.Callback = callback;
            RebindOwner(owner);
            CapturePersistedIdentity();
        }

        internal void OnRestored()
        {
            onRestored?.Invoke();
        }

        public void SetEnabled(bool enabled) => isEnabled = enabled;
        public void SetActiveInEditor(bool activeInEditor) => this.activeInEditor = activeInEditor;

        internal void RebindOwner(InstancedEventBaseOpaque newOwner)
        {
            owner = newOwner;
        }

        internal InstancedEventListenerPersistedState CreatePersistedState()
        {
            if (Callback == null) return null;

            CapturePersistedIdentity();
            var method = Callback.Method;
            var target = Callback.Target;
            var targetObject = target as UnityEngine.Object;

            return new InstancedEventListenerPersistedState
            {
                UniqueId = this.uniqueId, // <-- Save the ID
                DelegateTypeName = Callback.GetType().AssemblyQualifiedName,
                DeclaringTypeName = method?.DeclaringType?.AssemblyQualifiedName ?? method?.ReflectedType?.AssemblyQualifiedName,
                MethodName = method?.Name,
                TargetTypeName = target?.GetType().AssemblyQualifiedName,
                TargetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0,
                Priority = Priority,
                IsEnabled = isEnabled
            };
        }

        public void Destroy()
        {
            isEnabled = false;

            InstancedEventHandler.RemoveListenerFromAllEvents(this); 
            if (owner != null)
            {
                owner.Remove(this);
                owner = null;
                return;
            }
        }

        private void CapturePersistedIdentity()
        {
            var method = Callback?.Method;
            var target = Callback?.Target;
            var targetObject = target as UnityEngine.Object;

            persistedDelegateTypeName = Callback?.GetType().AssemblyQualifiedName;
            persistedDeclaringTypeName = method?.DeclaringType?.AssemblyQualifiedName ?? method?.ReflectedType?.AssemblyQualifiedName;
            persistedMethodName = method?.Name;
            persistedTargetTypeName = target?.GetType().AssemblyQualifiedName;
            persistedTargetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0;
        }

        internal bool HasPersistedIdentity()
        {
            return !string.IsNullOrEmpty(persistedMethodName) || !string.IsNullOrEmpty(persistedTargetTypeName) || persistedTargetInstanceId != 0;
        }

        internal bool MatchesPersistedIdentity(ListenerHandle other)
        {
            if (other == null) return false;

            if (!string.IsNullOrEmpty(uniqueId) && !string.IsNullOrEmpty(other.uniqueId))
            {
                return string.Equals(uniqueId, other.uniqueId, StringComparison.Ordinal);
            }

            if (!string.Equals(persistedMethodName, other.persistedMethodName, StringComparison.Ordinal)) return false;
            if (!string.Equals(persistedDeclaringTypeName, other.persistedDeclaringTypeName, StringComparison.Ordinal)) return false;
            if (!string.Equals(persistedTargetTypeName, other.persistedTargetTypeName, StringComparison.Ordinal)) return false;
            if (persistedTargetInstanceId != 0 && other.persistedTargetInstanceId != 0)
            {
                return persistedTargetInstanceId == other.persistedTargetInstanceId;
            }

            return Equals(Callback?.Target, other.Callback?.Target);
        }

        public ListenerHandle BindTo(MonoBehaviour owner)
        {
            owner.destroyCancellationToken.Register(Destroy);
            return this;
        }
    }

    [System.Serializable]
    public abstract class InstancedEventBaseOpaque
    {
        public class ListenerPriorityComparer : IComparer<ListenerHandle>
        {
            public int Compare(ListenerHandle x, ListenerHandle y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return 1;
                if (y == null) return -1;

                int priorityComparison = y.Priority.CompareTo(x.Priority);

                if (priorityComparison == 0)
                {
                    return y.GetHashCode().CompareTo(x.GetHashCode());
                }

                return priorityComparison;
            }
        }

        [SerializeField]
        protected List<ListenerHandle> instancesByPriority = new();

        internal void SaveToPersistence()
        {
            if (Application.isPlaying) return;

            var listeners = new List<InstancedEventListenerPersistedState>();
            for (int i = 0; i < instancesByPriority.Count; i++)
            {
                var handle = instancesByPriority[i];
                if (handle == null) continue;

                var state = handle.CreatePersistedState();
                if (state != null)
                {
                    listeners.Add(state);
                }
            }

            InstancedEventPersistence.SaveEventState(GetType(), listeners);
        }

        internal void RestoreFromPersistence()
        {
            instancesByPriority.Clear();

            var entry = InstancedEventPersistence.LoadEventState(GetType());
            if (entry == null || entry.Listeners == null) return;

            for (int i = 0; i < entry.Listeners.Count; i++)
            {
                var state = entry.Listeners[i];
                if (state == null) continue;

                var callback = CreateDelegateFromPersistedState(state);
                if (callback == null) continue;

                var handle = new ListenerHandle(callback, this)
                {
                    Priority = state.Priority,
                    IsPlayModeListener = false,
                    uniqueId = string.IsNullOrEmpty(state.UniqueId) ? System.Guid.NewGuid().ToString() : state.UniqueId
                };

                handle.RebindOwner(this);
                handle.SetEnabled(state.IsEnabled);
                handle.OnRestored();
                instancesByPriority.Add(handle);
            }

            instancesByPriority.Sort(new ListenerPriorityComparer());
        }

        internal void ClearPlayModeListeners()
        {
            instancesByPriority.RemoveAll(handle => handle != null && handle.IsPlayModeListener);
        }

        public void ClearAllListeners()
        {
            for (int i = 0; i < instancesByPriority.Count; i++)
            {
                var handle = instancesByPriority[i];
                if (handle != null)
                {
                    handle.SetEnabled(false);
                }
            }

            instancesByPriority.Clear(); 

            if (!Application.isPlaying)
            {
                SaveToPersistence();
            }
        }

        internal void SendBase(System.Func<System.Delegate, ListenerResult> onCallback)
        {
            // Iterate over a snapshot so listeners can safely remove themselves during invocation
            var snapshot = new List<ListenerHandle>(instancesByPriority);
            foreach (var handle in snapshot)
            {
                if (handle == null || !handle.isEnabled || (!Application.isPlaying && !handle.activeInEditor)) continue;

                // Safety check: Prevent play mode listeners from executing in edit mode
                if (handle.IsPlayModeListener && !Application.isPlaying) continue;

                if (onCallback(handle.Callback) == ListenerResult.Block) break;
            }
        }

        internal ListenerHandle ListenBase(System.Delegate callback, int priority)
        {
            var listener = new ListenerHandle(callback, this)
            {
                Priority = priority,
                // Automatically flag as Play Mode if created while playing
                IsPlayModeListener = Application.isPlaying 
            };

            instancesByPriority.Add(listener);
            instancesByPriority.Sort(new ListenerPriorityComparer());

            if (!Application.isPlaying)
            {
                SaveToPersistence();
            }

            return listener;
        }

        internal void Remove(ListenerHandle handle)
        {
            RemoveMatchingListener(handle);
        }

        internal bool RemoveMatchingListener(ListenerHandle handle)
        {
            if (handle == null) return false;

            bool removed = false;
            for (int i = instancesByPriority.Count - 1; i >= 0; i--)
            {
                var existing = instancesByPriority[i];
                if (existing == null)
                {
                    continue;
                }

                if (!ReferenceEquals(existing, handle) && !HandlesMatch(existing, handle))
                {
                    continue;
                }

                existing.SetEnabled(false);
                instancesByPriority.RemoveAt(i);
                removed = true;
            }

            if (removed && !Application.isPlaying)
            {
                SaveToPersistence();
            }

            return removed;
        }

        private static bool HandlesMatch(ListenerHandle left, ListenerHandle right)
        {
            if (left == null || right == null) return false;
            if (ReferenceEquals(left, right)) return true;
            if (left.HasPersistedIdentity() || right.HasPersistedIdentity())
            {
                return left.MatchesPersistedIdentity(right);
            } 

            if (left.Callback == null || right.Callback == null) return false;
            if (!string.Equals(left.Callback.Method?.Name, right.Callback.Method?.Name, StringComparison.Ordinal)) return false;
            if (!string.Equals(left.Callback.Method?.DeclaringType?.AssemblyQualifiedName, right.Callback.Method?.DeclaringType?.AssemblyQualifiedName, StringComparison.Ordinal)) return false;
            return Equals(left.Callback.Target, right.Callback.Target);
        }

        private System.Delegate CreateDelegateFromPersistedState(InstancedEventListenerPersistedState state)
        {
            if (state == null || string.IsNullOrEmpty(state.DelegateTypeName) || string.IsNullOrEmpty(state.MethodName))
            {
                return null;
            }

            var delegateType = ResolveType(state.DelegateTypeName);
            if (delegateType == null)
            {
                return null;
            }

            var declaringType = ResolveType(state.DeclaringTypeName);
            if (declaringType == null)
            {
                return null;
            }

            var targetObject = ResolveTargetObject(state);
            var method = FindMatchingMethod(declaringType, state.MethodName, delegateType);
            if (method == null && targetObject != null)
            {
                declaringType = targetObject.GetType();
                method = FindMatchingMethod(declaringType, state.MethodName, delegateType);
            }

            if (method == null)
            {
                return null;
            }

            try
            {
                if (targetObject == null)
                {
                    return Delegate.CreateDelegate(delegateType, method, false);
                }

                return Delegate.CreateDelegate(delegateType, targetObject, method, false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private UnityEngine.Object ResolveTargetObject(InstancedEventListenerPersistedState state)
        {
            if (state == null || string.IsNullOrEmpty(state.TargetTypeName) || state.TargetInstanceId == 0)
            {
                return null;
            }

            var targetType = ResolveType(state.TargetTypeName);
            if (targetType == null || !typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                return null;
            }

            var allTargets = Resources.FindObjectsOfTypeAll(targetType);
            for (int i = 0; i < allTargets.Length; i++)
            {
                var candidate = allTargets[i] as UnityEngine.Object;
                if (candidate != null && candidate.GetInstanceID() == state.TargetInstanceId)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MethodInfo FindMatchingMethod(Type declaringType, string methodName, Type delegateType)
        {
            if (declaringType == null || string.IsNullOrEmpty(methodName) || delegateType == null)
            {
                return null;
            }

            var invokeMethod = delegateType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                return null;
            }

            var delegateParameters = invokeMethod.GetParameters();
            var expectedReturnType = invokeMethod.ReturnType;
            var methods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != delegateParameters.Length)
                {
                    continue;
                }

                bool matches = true;
                for (int j = 0; j < parameters.Length; j++)
                {
                    if (!parameters[j].ParameterType.IsAssignableFrom(delegateParameters[j].ParameterType) &&
                        !delegateParameters[j].ParameterType.IsAssignableFrom(parameters[j].ParameterType))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                if (!expectedReturnType.IsAssignableFrom(method.ReturnType) && !method.ReturnType.IsAssignableFrom(expectedReturnType))
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static System.Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var type = System.Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }
    }

    [System.Serializable]
    public abstract class InstancedEventBase<TSelf> : InstancedEventBaseOpaque where TSelf : InstancedEventBaseOpaque
    {
        public InstancedEventBase()
        {
            if (!InstancedEventHandler.IsSpawning)
            {
                throw new System.InvalidOperationException($"Cannot use 'new' to create instanced events. Attempted to create new instance of {typeof(TSelf).Name}");
            }
        }

        internal static TSelf Get()
        {
            return InstancedEventHandler.GetSharedEventInstance<TSelf>();
        }
    }

    [System.Serializable]
    public abstract class InstancedEvent<TSelf> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf>
    {
        public static void Send() => Get().MSend();

        protected virtual void MSend()
        {
            SendBase(OnDelegate);
        }

        protected virtual ListenerResult OnDelegate(System.Delegate dele)
        {
            if (dele is System.Func<ListenerResult> func)
                return func();
                
            return ListenerResult.Propagate;
        }

        public static ListenerHandle Listen(System.Func<ListenerResult> callback, int priority = 0) => Get().MListen(callback, priority);

        protected virtual ListenerHandle MListen(System.Func<ListenerResult> callback, int priority = 0)
        {
            return ListenBase(callback, priority);
        }
    }

    [System.Serializable]
    public abstract class InstancedEvent<TSelf, T1> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1>
    {
        public static void Send(T1 param1) => Get().MSend(param1);

        protected virtual void MSend(T1 param1)
        {
            SendBase(dele => OnDelegate(dele, param1));
        }

        protected virtual ListenerResult OnDelegate(System.Delegate dele, T1 param1)
        {
            if (dele is System.Func<T1, ListenerResult> func)
                return func(param1);
            
            return ListenerResult.Propagate;
        }

        public static ListenerHandle Listen(System.Func<T1, ListenerResult> callback, int priority = 0) => Get().MListen(callback, priority);

        protected virtual ListenerHandle MListen(System.Func<T1, ListenerResult> callback, int priority = 0)
        {
            return ListenBase(callback, priority);
        }
    }

    [System.Serializable]
    public abstract class InstancedEvent<TSelf, T1, T2> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1, T2>
    {
        public static void Send(T1 param1, T2 param2) => Get().MSend(param1, param2);

        protected virtual void MSend(T1 param1, T2 param2)
        {
            SendBase(dele => OnDelegate(dele, param1, param2));
        }

        protected virtual ListenerResult OnDelegate(System.Delegate dele, T1 param1, T2 param2)
        {
            if (dele is System.Func<T1, T2, ListenerResult> func)
                return func(param1, param2);

            return ListenerResult.Propagate;
        }

        public static ListenerHandle Listen(System.Func<T1, T2, ListenerResult> callback, int priority = 0) => Get().MListen(callback, priority);

        protected virtual ListenerHandle MListen(System.Func<T1, T2, ListenerResult> callback, int priority = 0)
        {
            return ListenBase(callback, priority);
        }
    }

    [System.Serializable]
    public abstract class InstancedEvent<TSelf, T1, T2, T3> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1, T2, T3>
    {
        public static void Send(T1 param1, T2 param2, T3 param3) => Get().MSend(param1, param2, param3);

        protected virtual void MSend(T1 param1, T2 param2, T3 param3)
        {
            SendBase(dele => OnDelegate(dele, param1, param2, param3));
        }

        protected virtual ListenerResult OnDelegate(System.Delegate dele, T1 param1, T2 param2, T3 param3)
        {
            if (dele is System.Func<T1, T2, T3, ListenerResult> func)
                return func(param1, param2, param3);
            
            return ListenerResult.Propagate;
        }

        public static ListenerHandle Listen(System.Func<T1, T2, T3, ListenerResult> callback, int priority = 0) => Get().MListen(callback, priority);

        protected virtual ListenerHandle MListen(System.Func<T1, T2, T3, ListenerResult> callback, int priority = 0)
        {
            return ListenBase(callback, priority);
        }
    }

    [System.Serializable]
    public abstract class InstancedEvent<TSelf, T1, T2, T3, T4> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1, T2, T3, T4>
    {
        public static void Send(T1 param1, T2 param2, T3 param3, T4 param4) => Get().MSend(param1, param2, param3, param4);

        protected virtual void MSend(T1 param1, T2 param2, T3 param3, T4 param4)
        {
            SendBase(dele => OnDelegate(dele, param1, param2, param3, param4));
        }

        protected virtual ListenerResult OnDelegate(System.Delegate dele, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (dele is System.Func<T1, T2, T3, T4, ListenerResult> func)
                return func(param1, param2, param3, param4);

            return ListenerResult.Propagate;
        }

        public static ListenerHandle Listen(System.Func<T1, T2, T3, T4, ListenerResult> callback, int priority = 0) => Get().MListen(callback, priority);

        protected virtual ListenerHandle MListen(System.Func<T1, T2, T3, T4, ListenerResult> callback, int priority = 0)
        {
            return ListenBase(callback, priority);
        }
    }
}