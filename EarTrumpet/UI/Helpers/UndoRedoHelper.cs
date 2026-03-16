using EarTrumpet.DataModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Applies undo/redo VolumeActions by finding the target device or app
    /// in the current CollectionViewModel and setting its volume/mute state.
    /// </summary>
    public static class UndoRedoHelper
    {
        public static void ApplyAction(VolumeUndoService.VolumeAction action, bool isUndo)
        {
            var collection = ((App)Application.Current).CollectionViewModel;
            if (collection == null) return;

            // Suppress undo recording while applying undo/redo values
            App.UndoService.BeginUndoRedo();
            try
            {
                if (action.IsDevice)
                {
                    var device = collection.AllDevices.FirstOrDefault(d => d.Id == action.TargetId);
                    if (device == null)
                    {
                        Trace.WriteLine($"UndoRedoHelper: Device '{action.DisplayName}' ({action.TargetId}) not found, skipping.");
                        return;
                    }

                    if (action.OldMuted.HasValue)
                    {
                        device.IsMuted = isUndo ? action.OldMuted.Value : action.NewMuted.Value;
                    }
                    else
                    {
                        device.Volume = isUndo ? action.OldVolume : action.NewVolume;
                    }
                }
                else
                {
                    // Search all devices for the app session
                    foreach (var device in collection.AllDevices)
                    {
                        var appItem = device.Apps.FirstOrDefault(a => a.Id == action.TargetId);
                        if (appItem != null)
                        {
                            if (action.OldMuted.HasValue)
                            {
                                appItem.IsMuted = isUndo ? action.OldMuted.Value : action.NewMuted.Value;
                            }
                            else
                            {
                                appItem.Volume = isUndo ? action.OldVolume : action.NewVolume;
                            }
                            return;
                        }
                    }
                    Trace.WriteLine($"UndoRedoHelper: App '{action.DisplayName}' ({action.TargetId}) not found, skipping.");
                }
            }
            finally
            {
                App.UndoService.EndUndoRedo();
            }
        }
    }
}
