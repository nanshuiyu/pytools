﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.PythonTools.Intellisense {
    class SmartTagController : IIntellisenseController {
        private readonly ISmartTagBroker _broker;
        private readonly ITextView _textView;
        private ISmartTagSession _curSession;
        private bool _sessionIsInvalid;
        private SmartTagSource.AbortedAugmentInfo _abortedAugment;

        public SmartTagController(ISmartTagBroker broker, ITextView textView) {
            _broker = broker;
            _textView = textView;
            _textView.Caret.PositionChanged += Caret_PositionChanged;
            _sessionIsInvalid = true;
        }

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e) {
            _sessionIsInvalid = true;
            _abortedAugment = null;
        }

        static internal SmartTagController CreateInstance(ISmartTagBroker broker, ITextView textView, IList<ITextBuffer> subjectBuffers) {
            Type key = typeof(SmartTagController);

            SmartTagController controller = null;
            if (textView.Properties.TryGetProperty(key, out controller)) {
                return controller;
            }

            controller = new SmartTagController(broker, textView);
            textView.Properties.AddProperty(key, controller);

            foreach (var buffer in subjectBuffers) {
                buffer.ChangedLowPriority += controller.SubjectBufferChangedLowPriority;
            }

            return controller;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.ChangedLowPriority += SubjectBufferChangedLowPriority;
        }

        void SubjectBufferChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            _sessionIsInvalid = true;
            _abortedAugment = null;
        }

        public void Detach(ITextView textView) {
            _textView.Caret.PositionChanged -= Caret_PositionChanged;
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.ChangedLowPriority -= SubjectBufferChangedLowPriority;
        }

        internal void ShowSmartTag(IOleComponentManager compMgr = null) {
            if (!_sessionIsInvalid) {
                // caret / text hasn't changed since we last computed the smart tag, don't bother computing again.
                return;
            }

            _sessionIsInvalid = false;

            // Figure out the point in the buffer where we are triggering.
            // We need to use the view's data buffer as the source location
            if (_curSession != null && !_curSession.IsDismissed) {
                _curSession.Dismiss();
            }

            ITextSnapshot snapshot = _textView.TextViewModel.DataBuffer.CurrentSnapshot;
            SnapshotPoint? caretPoint = _textView.Caret.Position.Point.GetPoint(snapshot, PositionAffinity.Successor);

            if (!caretPoint.HasValue) {
                return;
            }

            ITrackingPoint triggerPoint = snapshot.CreateTrackingPoint(caretPoint.Value, PointTrackingMode.Positive);

            ISmartTagSession newSession = _curSession = _broker.CreateSmartTagSession(_textView, SmartTagType.Factoid, triggerPoint, SmartTagState.Collapsed);
            newSession.Properties.AddProperty(typeof(SmartTagController), compMgr);
            if (_abortedAugment != null) {
                newSession.Properties.AddProperty(SmartTagSource.AbortedAugment, _abortedAugment);
            }
            newSession.Start();

            SmartTagSource.AbortedAugmentInfo abortInfo;
            if (newSession.Properties.TryGetProperty<SmartTagSource.AbortedAugmentInfo>(SmartTagSource.AbortedAugment, out abortInfo) && abortInfo != _abortedAugment) {
                // we didn't process all of the invalid imports
                _sessionIsInvalid = true;
                _abortedAugment = abortInfo;
            } else {
                _abortedAugment = null;
            }
        }
    }
}
