using AmbientServices.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// A class that mananges writing status notificatoins.
    /// </summary>
    internal class StatusNotificationWriter
    {
#if RAWRATINGS
        private const string DebugRatingFloatFormat = "0.00";
#endif
        private const string HtmlTabStart = "<div style=\"margin-left:10px\">";
        private const string HtmlTabEnd = "</div>";
        private static readonly string[] RenderLevelStrings = new string[] { "h0", "h1", "h2", "h3", "h4" };    // note that h0 doesn't get used
        private static string RenderLevelString(int level)
        {
            return (level >= RenderLevelStrings.Length) ? "div" : RenderLevelStrings[level];
        }
        private static string RenderTerse(string terse) { return terse; }
        private static string RenderDetails(string details) { return details; }

        private readonly StringBuilder _details;
        private readonly StringBuilder _terse;
        private int _tabLevel;
        private StatusRatingRange _currentSectionRatingRange;
        private DateTime? _notificationTime;

        /// <summary>
        /// Get the detailed notificaton (so far).
        /// </summary>
        public string Details => _details.ToString();
        /// <summary>
        /// Get the terse notification (so far).
        /// </summary>
        public string Terse => _terse.ToString();

        /// <summary>
        /// Constructs a status notification writer with the specified notification time.
        /// </summary>
        /// <param name="notificationTime">The notification time.  Defaults to the current time.</param>
        public StatusNotificationWriter(DateTime? notificationTime = null)
        {
            _tabLevel = 1;
            _currentSectionRatingRange = (StatusRatingRange)(-1);
            _details = new StringBuilder();
            _terse = new StringBuilder();
            _notificationTime = notificationTime ?? AmbientClock.UtcNow;
        }

        private static void OpenHeader(StringBuilder details, StringBuilder terse, int tabLevel, StatusRatingRange range = (StatusRatingRange)(-1), string? color = null)
        {
            if (terse != null)
            {
                terse.Append(new string(' ', tabLevel - 1));
                if (range != (StatusRatingRange)(-1))
                {
                    string symbolPrefix = StatusRating.GetRangeSymbol(range) + " ";
                    terse.Append(symbolPrefix);
                }
            }
            details.Append('<');
            details.Append(RenderLevelString(tabLevel));
            if (!string.IsNullOrEmpty(color)) details.Append(" style=\"color:" + color + "\"");
            details.Append('>');
        }
        private static void CloseHeader(StringBuilder details, StringBuilder terse, int tabLevel)
        {
            if (terse != null) terse.AppendLine();
            details.Append("</");
            details.Append(RenderLevelString(tabLevel));
            details.Append('>');
        }
        private void EnterTabLevel()
        {
            _details.Append(HtmlTabStart);
            ++_tabLevel;
        }
        private void LeaveTabLevel()
        {
            --_tabLevel;
            _details.Append(HtmlTabEnd);
        }
        /// <summary>
        /// Enters the html and body elements, with a backround color set if the overall rating is specified.  
        /// Should be matched my a subsequent to <see cref="LeaveBodyAndHtml"/>.
        /// </summary>
        /// <param name="overallRating">The overall rating to use for the background color.</param>
        public void EnterHtmlAndBody(float? overallRating = null)
        {
            _details.Append(
@"<html>
    <head>");
            if (overallRating != null)
            {
                string backgroundColor = StatusRating.GetRangeBackgroundColor(overallRating.Value);
                _details.Append(@"
    <style>");
                _details.Append(StatusRating.StyleDefinition);
                _details.Append(@"
    body{background-color:" + backgroundColor + @";}
    </style>");
            }
            _details.Append(@"
    </head>
    <body>
");
        }
        /// <summary>
        /// Leaves the body and html tags entered in a previous call to <see cref="EnterHtmlAndBody"/>.
        /// </summary>
        public void LeaveBodyAndHtml()
        {
            _details.Append(@"
    </body>
</html>
");
        }
        /// <summary>
        /// Enters a status range section for the range indicated by the rating.
        /// Should be matched by a subsequent call to <see cref="LeaveStatusRange"/>.
        /// </summary>
        /// <param name="rating">The status rating.</param>
        public void EnterStatusRange(float rating)
        {
            StatusRatingRange range = StatusRating.FindRange(rating);
            string rangeName = StatusRating.GetRangeName(range);
            string rangeColor = StatusRating.GetRangeForegroundColor(range);

            if (_tabLevel > 1) throw new InvalidOperationException("All targets and status ranges must be closed before entering a new status range!");

            _details.Append("<div class=\"");
#pragma warning disable CA1308 // this is to convert the range name from the C# style casing (Pascal) to the HTML style casing (kebab)
            _details.Append(rangeName.ToLowerInvariant());
#pragma warning restore CA1308
            _details.Append("-range\">");

            OpenHeader(_details, _terse, _tabLevel, range, rangeColor);
            _currentSectionRatingRange = range;

            _terse.Append(rangeName.ToUpperInvariant());
            _details.Append(StatusRating.GetRangeSymbol(range));
            _details.Append(' ');
            _details.Append(rangeName);

            if (_notificationTime != null)
            {
                _terse.Append(" @");
                _terse.Append(_notificationTime.Value.ToShortTimeString());
                _details.Append(" at ");
                _details.Append(_notificationTime.Value.ToLongTimeString());
                _notificationTime = null;
            }

            CloseHeader(_details, _terse, _tabLevel);

            EnterTabLevel();
        }
        /// <summary>
        /// Leaves a status range started by a call to <see cref="EnterStatusRange"/>.
        /// </summary>
        public void LeaveStatusRange()
        {
            if (_tabLevel != 2) throw new InvalidOperationException("There is no status range to leave!");
            LeaveTabLevel();
            _details.Append("</div>");
        }
        /// <summary>
        /// Enters a target section.  Should be matched by a subsequent call to <see cref="LeaveTarget"/>.
        /// </summary>
        /// <param name="target">The name of the target owning this section.</param>
        /// <param name="rating">The overall status rating for this target.</param>
        public void EnterTarget(string target, float rating)
        {
            if (_tabLevel < 2) throw new InvalidOperationException("A status range must be entered before a target is!");

            string rgbColor = StatusRating.GetRatingRgbForegroundColor(rating);
            StatusRatingRange range = StatusRating.FindRange(rating);
            OpenHeader(_details, _terse, _tabLevel, (StatusRatingRange)(-1), rgbColor);
            _terse.Append(target);
            _details.Append(target);
            CloseHeader(_details, _terse, _tabLevel);
            EnterTabLevel();
        }

        /// <summary>
        /// Leaves a target section entered by a previous call to <see cref="EnterTarget"/>.
        /// </summary>
        public void LeaveTarget()
        {
            if (_tabLevel < 3) throw new InvalidOperationException("There is no target to leave!");

            LeaveTabLevel();
        }
        /// <summary>
        /// Writes a notification for the specified <see cref="AggregatedAlert"/>.
        /// </summary>
        /// <param name="aggregatedAlert">An <see cref="AggregatedAlert"/> that should be written to the notification.</param>
        public void WriteAggregatedAlert(AggregatedAlert aggregatedAlert)
        {
            if (_tabLevel <= 1) throw new InvalidOperationException("A status range must be entered before aggregated alerts can be written!");

            StatusAuditAlert auditAlert = aggregatedAlert.CommonAlert ?? StatusAuditAlert.None;
            StatusRatingRange range = StatusRating.FindRange(auditAlert.Rating);
            string rangeName = StatusRating.GetRangeName(range);
            string rangeColor = StatusRating.GetRangeForegroundColor(auditAlert.Rating);
            string rgbColor = StatusRating.GetRatingRgbForegroundColor(aggregatedAlert.RatingSum / aggregatedAlert.Sources.Count);

            List<StatusPropertyRange> propertyRanges = aggregatedAlert.PropertyRanges;

            OpenHeader(_details, _terse, _tabLevel, (StatusRatingRange)(-1), rgbColor);

            _terse.Append(aggregatedAlert.Target + ": " + aggregatedAlert.TerseSources + "->");

#if RAWRATINGS
            _details.Append("[RATING=" + auditAlert.Rating.ToString(DebugRatingFloatFormat) + "] ");
#endif
            _details.Append(aggregatedAlert.Target + ": " + aggregatedAlert.DetailsSources + " reporting: ");

            // multi-line alert details or properties to list?
            if ((aggregatedAlert.CommonAlert?.Details != null && (aggregatedAlert.CommonAlert.Details.Contains("<br/>", StringComparison.Ordinal) || aggregatedAlert.CommonAlert.Details.Contains("</div>", StringComparison.Ordinal))) || (propertyRanges != null && propertyRanges.Count > 0))
            {
                CloseHeader(_details, _terse, _tabLevel);

                EnterTabLevel();
                OpenHeader(_details, _terse, _tabLevel, (StatusRatingRange)(-1), rgbColor);

                _terse.Append(RenderTerse(auditAlert.Terse).Replace("\n", "\n" + new string(' ', _tabLevel), StringComparison.Ordinal));
                _details.AppendLine(RenderDetails(auditAlert.Details));

                CloseHeader(_details, _terse, _tabLevel);

                // are there properties?
                if (propertyRanges != null && propertyRanges.Count > 0)
                {
                    _details.Append(" because");
                    foreach (StatusPropertyRange propertyRange in aggregatedAlert.PropertyRanges)
                    {
                        OpenHeader(_details, _terse, _tabLevel, (StatusRatingRange)(-1), rgbColor);
                        _terse.Append(propertyRange.ToString());
                        _details.AppendLine(propertyRange.ToString());
                        CloseHeader(_details, _terse, _tabLevel);
                    }
                }
                LeaveTabLevel();
            }
            else
            {
                _terse.Append(RenderTerse(auditAlert.Terse));
                _details.Append(' ');
                _details.Append(RenderDetails(auditAlert.Details));
                CloseHeader(_details, _terse, _tabLevel);
            }
        }
    }
}
