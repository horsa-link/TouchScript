

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
#if TOUCHSCRIPT_DEBUG
using TouchScript.Debugging.Filters;
using TouchScript.Debugging.Loggers;
using TouchScript.InputSources.InputHandlers;
using TouchScript.Pointers;
using TouchScript.Utils;

#endif

namespace TouchScript.Debugging
{
    [DefaultExecutionOrder(-50)]
    public class DebugLogger : MonoBehaviour
#if TOUCHSCRIPT_DEBUG
        , IPointerLogger
#endif
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private int numLines = 10;
        
        /// <inheritdoc />
        public int PointerCount
        {
            get { throw new NotImplementedException("DebugLogger doesn't support reading data."); }
        }

        private List<string> lines = new();
        private StringBuilder linesBuilder = new();

#if TOUCHSCRIPT_DEBUG
        private void Awake()
        {
            TouchScriptDebugger.Instance.PointerLogger = this;
        }

        private void LateUpdate()
        {
            linesBuilder.Clear();
            
            foreach (var line in lines)
            {
                linesBuilder.AppendLine(line);
            }

            text.text = linesBuilder.ToString();
        }
#endif
        
#if TOUCHSCRIPT_DEBUG
        public void Log(Pointer pointer, PointerEvent evt)
        {
            var path = TransformUtils.GetHierarchyPath(pointer.GetPressData().Target);

            var inputSource = pointer.InputSource;
            var targetDisplay = "0";
            
            if (inputSource is IMultiWindowInputHandler multiWindowInputHandler)
            {
                targetDisplay = multiWindowInputHandler.TargetDisplay.ToString();
            }

            var line =
                $"Display {targetDisplay}: {pointer.Type} ({pointer.Id} | {pointer.Flags}): {pointer.Position.x},{pointer.Position.y} ({pointer.PreviousPosition.x},{pointer.PreviousPosition.y}) - ({path ?? "<None>"})";

            lines.Add(line);
            if (lines.Count > numLines)
            {
                lines.RemoveAt(0);
            }
        }

        public List<PointerData> GetFilteredPointerData(IPointerDataFilter filter = null)
        {
            throw new NotImplementedException("DebugLogger doesn't support reading data.");
        }

        public List<PointerLog> GetFilteredLogsForPointer(int id, IPointerLogFilter filter = null)
        {
            throw new NotImplementedException("DebugLogger doesn't support reading data.");
        }

        public void Dispose() { }
#endif
    }
}