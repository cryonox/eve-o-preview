using System;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;

namespace EveOPreview.UI.Hotkeys
{
	class HotkeyHandler : IMessageFilter, IDisposable
	{
		private static int _currentId;
		private const int MAX_ID = 0xBFFF;

		#region Private fields
		private bool _shouldNotUnregister;
		private readonly int _hotkeyId;
		private readonly IntPtr _hotkeyTarget;
		#endregion
		public static bool Enabled { get; set; } = true;
		public static List<HotkeyHandler> KnownHandlers= new List<HotkeyHandler>();
		public HotkeyHandler(IntPtr target, Keys hotkey,bool shouldNotUnregister=false)
		{
			this._hotkeyId = HotkeyHandler._currentId;
			HotkeyHandler._currentId = (HotkeyHandler._currentId + 1) & HotkeyHandler.MAX_ID;

			this._hotkeyTarget = target;

			// Assign properties
			this.IsRegistered = false;

			this.KeyCode = hotkey;
			_shouldNotUnregister = shouldNotUnregister;

            KnownHandlers.Add(this);
        }

		public void Dispose()
		{
			this.Unregister();
			GC.SuppressFinalize(this);
		}

		~HotkeyHandler()
		{
			// Unregister the hotkey if necessary
			KnownHandlers.Remove(this);

            this.Unregister();
		}
        public static void RegisterAll()
        {
            foreach (var handler in KnownHandlers)
            {
				handler.Register();
            }
        }
        public static void UnregisterAll()
		{
            foreach (var handler in KnownHandlers)
            {
				if(!handler._shouldNotUnregister)
					handler.Unregister();
            }
        }


        public bool IsRegistered { get; private set; }

		public Keys KeyCode { get; private set; }

		public event HandledEventHandler Pressed;

		public bool CanRegister()
		{
			// Attempt to register
			if (this.Register())
			{
				// Unregister and say we managed it
				this.Unregister();
				return true;
			}

			return false;
		}

		public bool Register()
		{
			// Check that we have not registered
			if (this.IsRegistered)
			{
				return false;
			}

			if (this.KeyCode == Keys.None)
			{
				return false;
			}

			// Remove all modifiers from the 'main' hotkey
			uint key = (uint)this.KeyCode & (~(uint)Keys.Alt) & (~(uint)Keys.Control) & (~(uint)Keys.Shift);

			// Get unmanaged version of the modifiers code
			uint modifiers = (this.KeyCode.HasFlag(Keys.Alt) ? HotkeyHandlerNativeMethods.MOD_ALT : 0)
							 | (this.KeyCode.HasFlag(Keys.Control) ? HotkeyHandlerNativeMethods.MOD_CONTROL : 0)
							 | (this.KeyCode.HasFlag(Keys.Shift) ? HotkeyHandlerNativeMethods.MOD_SHIFT : 0);

			// Register the hotkey
			if (!HotkeyHandlerNativeMethods.RegisterHotKey(this._hotkeyTarget, this._hotkeyId, modifiers, key))
			{
				return false;
			}

			Application.AddMessageFilter(this);

			this.IsRegistered = true;

			// We successfully registered
			return true;
		}

		public void Unregister()
		{
			// Check that we have registered
			if (!this.IsRegistered)
			{
				return;
			}

			this.IsRegistered = false;

			Application.RemoveMessageFilter(this);

			// Clean up after ourselves
			HotkeyHandlerNativeMethods.UnregisterHotKey(this._hotkeyTarget, this._hotkeyId);
		}

		#region IMessageFilter
		public bool PreFilterMessage(ref Message message)
		{
			return this.IsRegistered
					&& (message.Msg == HotkeyHandlerNativeMethods.WM_HOTKEY)
					&& (message.WParam.ToInt32() == this._hotkeyId)
					&& this.OnPressed();
		}
		#endregion

		private bool OnPressed()
        {
            // Fire the event if we can
            HandledEventArgs handledEventArgs = new HandledEventArgs(false);
            this.Pressed?.Invoke(this, handledEventArgs);

            // Return whether we handled the event or not
            return handledEventArgs.Handled;
        }
	}
}