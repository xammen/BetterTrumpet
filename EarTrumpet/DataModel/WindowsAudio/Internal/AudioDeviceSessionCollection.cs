using EarTrumpet.DataModel.Audio;
using EarTrumpet.Interop.MMDeviceAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace EarTrumpet.DataModel.WindowsAudio.Internal
{
    class AudioDeviceSessionCollection : IAudioSessionNotification
    {
        public ObservableCollection<IAudioDeviceSession> Sessions => _sessions;

        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<IAudioDeviceSession> _sessions = new ObservableCollection<IAudioDeviceSession>();
        private readonly List<IAudioDeviceSession> _movedSessions = new List<IAudioDeviceSession>();
        private IAudioSessionManager2 _sessionManager;
        private WeakReference<IAudioDevice> _parent;
        private volatile bool _isReleased;

        public AudioDeviceSessionCollection(IAudioDevice parent, IMMDevice device, Dispatcher foregroundDispatcher)
        {
            _parent = new WeakReference<IAudioDevice>(parent);
            _dispatcher = foregroundDispatcher;

            try
            {
                _sessionManager = device.Activate<IAudioSessionManager2>();
                _sessionManager.RegisterSessionNotification(this);
                var enumerator = _sessionManager.GetSessionEnumerator();
                int count = enumerator.GetCount();
                for (int i = 0; i < count; i++)
                {
                    CreateAndAddSession(enumerator.GetSession(i));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AudioDeviceSessionCollection Create dev={device.GetId()} {ex}");
            }
        }

        // Called by AudioDevice.Release on the UI thread. The former finalizer could never run while
        // the session notification was registered, and threw on a null manager when it did.
        public void Release()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;

            try
            {
                _sessionManager?.UnregisterSessionNotification(this);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AudioDeviceSessionCollection Release Failed {ex}");
            }

            foreach (var session in _sessions.ToArray())
            {
                ReleaseSession(session);
            }

            foreach (var session in _movedSessions.ToArray())
            {
                session.PropertyChanged -= MovedSession_PropertyChanged;
                ReleaseSession(session);
            }

            _movedSessions.Clear();
        }

        private void ReleaseSession(IAudioDeviceSession session)
        {
            if (session is AudioDeviceSessionGroup group)
            {
                foreach (var child in group.Sessions.ToArray())
                {
                    ReleaseSession(child);
                }
            }
            else if (session is AudioDeviceSession leaf)
            {
                leaf.PropertyChanged -= Session_PropertyChanged;
                leaf.Release();
            }
        }

        private void CreateAndAddSession(IAudioSessionControl session)
        {
            try
            {
                if (!_parent.TryGetTarget(out IAudioDevice parent))
                {
                    throw new Exception("Device session parent is invalid but device is still notifying.");
                }

                var newSession = new AudioDeviceSession(parent, session, _dispatcher);
                _dispatcher.BeginInvoke((Action)(() =>
                {
                    if (_isReleased)
                    {
                        newSession.Release();
                    }
                    else if (newSession.State == SessionState.Moved)
                    {
                        _movedSessions.Add(newSession);
                        newSession.PropertyChanged += MovedSession_PropertyChanged;
                    }
                    else if (newSession.State != SessionState.Expired)
                    {
                        AddSession(newSession);
                    }
                }));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AudioDeviceSessionCollection CreateAndAddSession {ex}");
            }
        }

        void IAudioSessionNotification.OnSessionCreated(IAudioSessionControl NewSession)
        {
            Trace.WriteLine($"AudioDeviceSessionCollection OnSessionCreated");
            if (_isReleased)
            {
                return;
            }

            CreateAndAddSession(NewSession);
        }

        private void AddSession(IAudioDeviceSession session)
        {
            Trace.WriteLine($"AudioDeviceSessionCollection AddSession {session.ExeName} {session.Id}");

            session.PropertyChanged += Session_PropertyChanged;

            // Windows can leave the previous endpoint's session alive when the
            // system default changes. The newly-created session on the current
            // default endpoint is authoritative for apps without an explicit
            // per-app route, so reconcile the old endpoint before grouping it.
            ReconcileDefaultMoveSources(session);

            if (_parent.TryGetTarget(out var parent))
            {
                if (session.IsSystemSoundsSession)
                {
                    AddSystemSoundsSession(parent, session);
                    return;
                }

                foreach (AudioDeviceSessionGroup appGroup in _sessions)
                {
                    if (appGroup.AppId == session.AppId)
                    {
                        foreach (AudioDeviceSessionGroup appSessionGroup in appGroup.Sessions)
                        {
                            if (appSessionGroup.GroupingParam == ((IAudioDeviceSessionInternal)session).GroupingParam)
                            {
                                // If there is a session in the same process, inherit safely.
                                // (Avoids a minesweeper ad playing at max volume when app should be muted)
                                session.IsMuted = session.IsMuted || appSessionGroup.IsMuted;
                                appSessionGroup.AddSession(session);
                                return;
                            }
                        }

                        session.IsMuted = session.IsMuted || appGroup.IsMuted;
                        appGroup.AddSession(new AudioDeviceSessionGroup(parent, session));
                        return;
                    }
                }

                _sessions.Add(new AudioDeviceSessionGroup(parent, new AudioDeviceSessionGroup(parent, session)));
            }
        }

        private void AddSystemSoundsSession(IAudioDevice parent, IAudioDeviceSession session)
        {
            var groupingParam = ((IAudioDeviceSessionInternal)session).GroupingParam;

            foreach (AudioDeviceSessionGroup appGroup in _sessions)
            {
                if (!appGroup.IsSystemSoundsSession || appGroup.GroupingParam != groupingParam)
                {
                    continue;
                }

                foreach (AudioDeviceSessionGroup appSessionGroup in appGroup.Sessions)
                {
                    if (appSessionGroup.GroupingParam == groupingParam)
                    {
                        session.IsMuted = session.IsMuted || appSessionGroup.IsMuted;
                        appSessionGroup.AddSession(session);
                        return;
                    }
                }

                session.IsMuted = session.IsMuted || appGroup.IsMuted;
                appGroup.AddSession(new AudioDeviceSessionGroup(parent, session));
                return;
            }

            _sessions.Add(new AudioDeviceSessionGroup(parent, new AudioDeviceSessionGroup(parent, session)));
        }

        internal void UnHideSessionsForProcessId(int processId)
        {
            foreach (var session in _movedSessions.ToArray())  // Use snapshot since enumeration will be modified.
            {
                if (session.ProcessId == processId)
                {
                    _movedSessions.Remove(session);
                    session.PropertyChanged -= MovedSession_PropertyChanged;

                    ((IAudioDeviceSessionInternal)session).UnHide();

                    AddSession(session);
                }
            }
        }

        internal void HideSessionsForDefaultMove(IAudioDeviceSession targetSession)
        {
            if (targetSession == null || targetSession.IsSystemSoundsSession)
            {
                return;
            }

            foreach (var appGroup in _sessions.OfType<AudioDeviceSessionGroup>().ToArray())
            {
                foreach (var sourceSession in EnumerateLeafSessions(appGroup).ToArray())
                {
                    if (IsImplicitDefaultMoveSource(sourceSession, targetSession))
                    {
                        Trace.WriteLine($"AudioDeviceSessionCollection HideDefaultMoveSource {sourceSession.ExeName} {sourceSession.Id} -> {targetSession.Parent.Id}");
                        ((IAudioDeviceSessionInternal)sourceSession).Hide();
                    }
                }
            }

            foreach (var sourceSession in _movedSessions.ToArray())
            {
                if (IsImplicitDefaultMoveSource(sourceSession, targetSession))
                {
                    Trace.WriteLine($"AudioDeviceSessionCollection HideDefaultMovedSource {sourceSession.ExeName} {sourceSession.Id} -> {targetSession.Parent.Id}");
                    ((IAudioDeviceSessionInternal)sourceSession).Hide();
                }
            }
        }

        private static void ReconcileDefaultMoveSources(IAudioDeviceSession targetSession)
        {
            if (targetSession == null ||
                targetSession.IsSystemSoundsSession ||
                targetSession.ProcessId <= 0 ||
                targetSession.Parent?.Parent is not IAudioDeviceManager manager ||
                !IsCurrentDefaultDevice(targetSession) ||
                HasPersistedRoute(targetSession))
            {
                return;
            }

            foreach (var device in manager.Devices.ToArray())
            {
                if (string.Equals(device.Id, targetSession.Parent.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (device is IAudioDeviceInternal internalDevice)
                {
                    internalDevice.HideSessionsForDefaultMove(targetSession);
                }
            }
        }

        private static bool IsCurrentDefaultDevice(IAudioDeviceSession session)
        {
            if (session?.Parent?.Parent is not IAudioDeviceManager manager || session.Parent == null)
            {
                return false;
            }

            if (manager.Default != null &&
                string.Equals(manager.Default.Id, session.Parent.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (manager is IAudioDeviceManagerWindowsAudio windowsManager)
            {
                try
                {
                    var currentDefault = windowsManager.GetDefaultDevice(ERole.eMultimedia);
                    return currentDefault != null &&
                           string.Equals(currentDefault.Id, session.Parent.Id, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"AudioDeviceSessionCollection DefaultDeviceQueryFailed {ex.Message}");
                }
            }

            return false;
        }

        private static bool HasPersistedRoute(IAudioDeviceSession session)
        {
            if (session?.Parent?.Parent is not IAudioDeviceManagerWindowsAudio manager)
            {
                return true;
            }

            try
            {
                return !string.IsNullOrWhiteSpace(manager.GetDefaultEndPoint(session.ProcessId));
            }
            catch (Exception ex)
            {
                // Do not hide a source when Windows cannot answer whether the
                // user configured an explicit per-app endpoint.
                Trace.WriteLine($"AudioDeviceSessionCollection PersistedRouteQueryFailed {ex.Message}");
                return true;
            }
        }

        private static bool IsImplicitDefaultMoveSource(IAudioDeviceSession sourceSession, IAudioDeviceSession targetSession)
        {
            if (sourceSession == null ||
                targetSession == null ||
                sourceSession.IsSystemSoundsSession ||
                targetSession.IsSystemSoundsSession ||
                sourceSession.Parent == null ||
                targetSession.Parent == null ||
                string.Equals(sourceSession.Parent.Id, targetSession.Parent.Id, StringComparison.OrdinalIgnoreCase) ||
                sourceSession.ProcessId <= 0 ||
                sourceSession.ProcessId != targetSession.ProcessId ||
                !IsSameApplication(sourceSession, targetSession))
            {
                return false;
            }

            return !HasPersistedRoute(targetSession);
        }

        private static bool IsSameApplication(IAudioDeviceSession sourceSession, IAudioDeviceSession targetSession)
        {
            if (!string.IsNullOrWhiteSpace(sourceSession.AppId) && !string.IsNullOrWhiteSpace(targetSession.AppId))
            {
                return string.Equals(sourceSession.AppId, targetSession.AppId, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(sourceSession.ExeName, targetSession.ExeName, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(sourceSession.ExeName, targetSession.ExeName, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<IAudioDeviceSession> EnumerateLeafSessions(IAudioDeviceSession session)
        {
            if (session.Children != null && session.Children.Count > 0)
            {
                foreach (var child in session.Children.ToArray())
                {
                    foreach (var leaf in EnumerateLeafSessions(child))
                    {
                        yield return leaf;
                    }
                }

                yield break;
            }

            yield return session;
        }

        public void MoveHiddenAppsToDevice(string appId, string id)
        {
            foreach (var session in _movedSessions)
            {
                if (session.AppId == appId)
                {
                    ((IAudioDeviceSessionInternal)session).MoveToDevice(id, false);
                }
            }
        }

        private void RemoveSession(IAudioDeviceSession session)
        {
            Trace.WriteLine($"AudioDeviceSessionCollection RemoveSession {session.ExeName} {session.Id}");

            session.PropertyChanged -= Session_PropertyChanged;

            foreach (AudioDeviceSessionGroup appGroup in _sessions)
            {
                foreach (AudioDeviceSessionGroup appSessionGroup in appGroup.Sessions)
                {
                    if (appSessionGroup.Sessions.Contains(session))
                    {
                        appSessionGroup.RemoveSession(session);

                        // Delete the now-empty app session group.
                        if (!appSessionGroup.Sessions.Any())
                        {
                            appGroup.RemoveSession(appSessionGroup);
                            break;
                        }
                    }
                }

                // Delete the now-empty app.
                if (!appGroup.Sessions.Any())
                {
                    _sessions.Remove(appGroup);
                    break;
                }
            }
        }

        private void Session_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var session = (IAudioDeviceSession)sender;

            if (e.PropertyName == nameof(session.State))
            {
                if (session.State == SessionState.Expired)
                {
                    RemoveSession(session);
                }
                else if (session.State == SessionState.Moved)
                {
                    RemoveSession(session);
                    _movedSessions.Add(session);
                    session.PropertyChanged += MovedSession_PropertyChanged;
                }
            }
        }

        private void MovedSession_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var session = (IAudioDeviceSession)sender;

            if (e.PropertyName == nameof(session.State) && session.State == SessionState.Active)
            {
                _movedSessions.Remove(session);
                session.PropertyChanged -= MovedSession_PropertyChanged;

                AddSession(session);
            }
        }
    }
}
