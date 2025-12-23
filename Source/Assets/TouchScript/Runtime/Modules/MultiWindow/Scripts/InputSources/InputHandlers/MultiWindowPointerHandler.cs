using System;
using System.Collections.Generic;
using TouchScript.Pointers;
using TouchScript.Utils;
using UnityEngine;

namespace TouchScript.InputSources.InputHandlers
{
    public abstract class MultiWindowPointerHandler : IMultiWindowInputHandler, IDisposable
    {
        public virtual int TargetDisplay
        {
            get => targetDisplay;
            set => targetDisplay = value;
        }
        
        /// <inheritdoc />
        public ICoordinatesRemapper CoordinatesRemapper { get; set; }
        
        protected readonly PointerDelegate addPointer;
        protected readonly PointerDelegate updatePointer;
        protected readonly PointerDelegate pressPointer;
        protected readonly PointerDelegate releasePointer;
        protected readonly PointerDelegate removePointer;
        protected readonly PointerDelegate cancelPointer;
        
        protected ObjectPool<TouchPointer> touchPool;
        protected ObjectPool<MousePointer> mousePool;
        protected ObjectPool<PenPointer> penPool;
        protected MousePointer mousePointer;
        protected PenPointer penPointer;

        protected int targetDisplay;

        public MultiWindowPointerHandler(int targetDisplay, PointerDelegate addPointer, PointerDelegate updatePointer,
            PointerDelegate pressPointer, PointerDelegate releasePointer, PointerDelegate removePointer,
            PointerDelegate cancelPointer)
        {
            this.targetDisplay = targetDisplay;
            
            this.addPointer = addPointer;
            this.updatePointer = updatePointer;
            this.pressPointer = pressPointer;
            this.releasePointer = releasePointer;
            this.removePointer = removePointer;
            this.cancelPointer = cancelPointer;
            
            touchPool = new ObjectPool<TouchPointer>(10, () => new TouchPointer(this), null, resetPointer);
        }
        
        public abstract void Dispose();
        
        /// <inheritdoc />
        public virtual bool UpdateInput()
        {
            return false;
        }

        /// <inheritdoc />
        public virtual void UpdateResolution()
        {
            setScaling();
            if (mousePointer != null) TouchManager.Instance.CancelPointer(mousePointer.Id);
        }

        /// <inheritdoc />
        public virtual void UpdateWindow() {}

        /// <inheritdoc />
        public abstract bool CancelPointer(Pointer pointer, bool shouldReturn);

        /// <inheritdoc />
        public virtual void INTERNAL_DiscardPointer(Pointer pointer)
        {
            var p = pointer as TouchPointer;
            if (p == null) return;

            touchPool.Release(p);
        }
        
        protected TouchPointer internalAddTouchPointer(Vector2 position)
        {
            var pointer = touchPool.Get();
            pointer.Position = remapCoordinates(position);
            pointer.Buttons |= Pointer.PointerButtonState.FirstButtonDown | Pointer.PointerButtonState.FirstButtonPressed;
            addPointer(pointer);
            pressPointer(pointer);
            return pointer;
        }

        protected TouchPointer internalReturnTouchPointer(TouchPointer pointer)
        {
            var newPointer = touchPool.Get();
            newPointer.CopyFrom(pointer);
            pointer.Buttons |= Pointer.PointerButtonState.FirstButtonDown | Pointer.PointerButtonState.FirstButtonPressed;
            newPointer.Flags |= Pointer.FLAG_RETURNED;
            addPointer(newPointer);
            pressPointer(newPointer);
            return newPointer;
        }

        protected void internalRemoveTouchPointer(TouchPointer pointer)
        {
            pointer.Buttons &= ~Pointer.PointerButtonState.FirstButtonPressed;
            pointer.Buttons |= Pointer.PointerButtonState.FirstButtonUp;
            releasePointer(pointer);
            removePointer(pointer);
        }

        protected MousePointer internalAddMousePointer(Vector2 position)
        {
            var pointer = mousePool.Get();
            pointer.Position = remapCoordinates(position);
            addPointer(pointer);
            return pointer;
        }

        protected MousePointer internalReturnMousePointer(MousePointer pointer)
        {
            var newPointer = mousePool.Get();
            newPointer.CopyFrom(pointer);
            newPointer.Flags |= Pointer.FLAG_RETURNED;
            addPointer(newPointer);
            if ((newPointer.Buttons & Pointer.PointerButtonState.AnyButtonPressed) != 0)
            {
                // Adding down state this frame
                newPointer.Buttons = PointerUtils.DownPressedButtons(newPointer.Buttons);
                pressPointer(newPointer);
            }
            return newPointer;
        }

        protected PenPointer internalAddPenPointer(Vector2 position)
        {
            if (penPointer != null) throw new InvalidOperationException("One pen pointer is already registered! Trying to add another one.");
            var pointer = penPool.Get();
            pointer.Position = remapCoordinates(position);
            addPointer(pointer);
            return pointer;
        }

        protected void internalRemovePenPointer(PenPointer pointer)
        {
            removePointer(pointer);
            penPointer = null;
        }

        protected PenPointer internalReturnPenPointer(PenPointer pointer)
        {
            var newPointer = penPool.Get();
            newPointer.CopyFrom(pointer);
            newPointer.Flags |= Pointer.FLAG_RETURNED;
            addPointer(newPointer);
            if ((newPointer.Buttons & Pointer.PointerButtonState.AnyButtonPressed) != 0)
            {
                // Adding down state this frame
                newPointer.Buttons = PointerUtils.DownPressedButtons(newPointer.Buttons);
                pressPointer(newPointer);
            }
            return newPointer;
        }
        
        protected Vector2 remapCoordinates(Vector2 position)
        {
            if (CoordinatesRemapper != null) return CoordinatesRemapper.Remap(position);
            return position;
        }

        protected void resetPointer(Pointer p)
        {
            p.INTERNAL_Reset();
        }

        protected abstract void setScaling();
    }
}
