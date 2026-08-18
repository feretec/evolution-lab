using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>Asset-free runtime observation UI for long-running worlds.</summary>
    public sealed class EvolutionLabUI : MonoBehaviour
    {
        private EvolutionSimulation simulation;
        private Creature selectedCreature;
        private GUIStyle panelStyle, emphasisPanelStyle, headerStyle, sectionStyle, labelStyle, valueStyle, smallStyle, mutedStyle, buttonStyle, selectedButtonStyle, warningButtonStyle, graphStyle, wrapStyle;
        private Material graphLineMaterial;
        private Vector2 controlsScrollPosition, selectedScrollPosition;
        private Rect statsRect, controlsRect, historyRect, selectedRect, compactNavigationRect;
        private bool ecologyExpanded = true, historyExpanded = true, historyPanelExpanded = true, tuningExpanded, cameraExpanded, archiveExpanded, selectionExpanded = true;
        private int compactTab;

        private static readonly Color Accent = new Color(0.20f, 0.78f, 0.82f, 1f);
        private static readonly Color AccentDim = new Color(0.12f, 0.37f, 0.41f, 1f);
        private static readonly Color Text = new Color(0.88f, 0.91f, 0.92f, 1f);
        private static readonly Color TextMuted = new Color(0.58f, 0.65f, 0.67f, 1f);
        private static readonly Color Success = new Color(0.39f, 0.82f, 0.58f, 1f);
        private static readonly Color Warning = new Color(0.94f, 0.67f, 0.35f, 1f);

        public void Bind(EvolutionSimulation owner) => simulation = owner;

        public void SetSelectedCreature(Creature creature)
        {
            selectedCreature = creature;
            selectionExpanded = true;
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return statsRect.Contains(guiPosition) || controlsRect.Contains(guiPosition) || historyRect.Contains(guiPosition) || selectedRect.Contains(guiPosition) || compactNavigationRect.Contains(guiPosition);
        }

        private void OnGUI()
        {
            if (simulation == null) return;
            EnsureStyles();
            float width = Mathf.Max(320f, Screen.width);
            float height = Mathf.Max(240f, Screen.height);
            float margin = width < 700f ? 10f : 16f;
            float gap = width < 700f ? 8f : 12f;
            statsRect = new Rect(margin, margin, width - margin * 2f, width < 700f ? 208f : 196f);
            bool compact = width < 700f;
            bool medium = !compact && width < 980f;
            compactNavigationRect = Rect.zero;
            if (compact)
            {
                float panelWidth = width - margin * 2f;
                compactNavigationRect = new Rect(margin, statsRect.yMax + gap, panelWidth, 30f);
                Rect activePanel = new Rect(
                    margin,
                    compactNavigationRect.yMax + gap,
                    panelWidth,
                    Mathf.Max(120f, height - compactNavigationRect.yMax - gap - margin));
                controlsRect = compactTab == 0 ? activePanel : Rect.zero;
                historyRect = compactTab == 1 ? activePanel : Rect.zero;
                selectedRect = compactTab == 2 ? activePanel : Rect.zero;
            }
            else if (medium)
            {
                float controlsWidth = Mathf.Clamp(width * 0.39f, 285f, 360f);
                float mainWidth = width - margin * 2f - controlsWidth - gap;
                float availableHeight = height - statsRect.yMax - gap - margin;
                controlsRect = new Rect(margin, statsRect.yMax + gap, controlsWidth, availableHeight);
                historyRect = new Rect(margin + controlsWidth + gap, statsRect.yMax + gap, mainWidth, Mathf.Clamp(availableHeight * 0.38f, 180f, 270f));
                selectedRect = new Rect(margin + controlsWidth + gap, historyRect.yMax + gap, mainWidth, Mathf.Max(120f, availableHeight - historyRect.height - gap));
            }
            else
            {
                float controlsWidth = Mathf.Clamp(width * 0.30f, 310f, 378f);
                float mainWidth = width - margin * 2f - controlsWidth - gap;
                controlsRect = new Rect(margin + mainWidth + gap, statsRect.yMax + gap, controlsWidth, height - statsRect.yMax - margin - gap);
                historyRect = new Rect(margin, statsRect.yMax + gap, mainWidth, Mathf.Clamp(height * 0.40f, 220f, 300f));
                selectedRect = new Rect(margin, historyRect.yMax + gap, mainWidth, height - historyRect.yMax - margin - gap);
            }
            DrawStatistics();
            if (compact)
            {
                DrawCompactNavigation(compactNavigationRect);
            }
            if (historyRect.width > 0f) DrawHistory();
            if (controlsRect.width > 0f) DrawControls();
            if (selectedRect.width > 0f) DrawSelectedCreature();
        }

        private void DrawCompactNavigation(Rect rect)
        {
            float width = (rect.width - 8f) / 3f;
            if (GUI.Button(new Rect(rect.x, rect.y, width, rect.height), "Controls", compactTab == 0 ? selectedButtonStyle : buttonStyle)) compactTab = 0;
            if (GUI.Button(new Rect(rect.x + width + 4f, rect.y, width, rect.height), "History", compactTab == 1 ? selectedButtonStyle : buttonStyle)) compactTab = 1;
            if (GUI.Button(new Rect(rect.x + (width + 4f) * 2f, rect.y, width, rect.height), "Selected", compactTab == 2 ? selectedButtonStyle : buttonStyle)) compactTab = 2;
        }

        private void DrawStatistics()
        {
            GUI.Box(statsRect, GUIContent.none, emphasisPanelStyle);
            float pad = statsRect.width < 700f ? 12f : 18f;
            Rect content = new Rect(statsRect.x + pad, statsRect.y + 11f, statsRect.width - pad * 2f, statsRect.height - 22f);
            GUI.Label(new Rect(content.x, content.y, content.width, 24f), "EVOLUTION LAB", headerStyle);
            GUI.Label(new Rect(content.x, content.y + 24f, content.width, 17f), "ARTIFICIAL LIFE / NATURAL HISTORY", mutedStyle);
            float colWidth = content.width > 690f ? content.width / 3f : content.width / 2f;
            DrawStatusValue(content.x, content.y + 50f, colWidth, "GENERATION", simulation.Generation.ToString(), Accent);
            DrawStatusValue(content.x + colWidth, content.y + 50f, colWidth, "POPULATION", simulation.PopulationCount + " / " + simulation.CarryingCapacity, Text);
            DrawStatusValue(content.x + colWidth * 2f, content.y + 50f, colWidth, "BEST FITNESS", simulation.BestFitness.ToString("0.000"), Success);
            if (content.width <= 690f)
            {
                DrawStatusValue(content.x, content.y + 91f, colWidth, "AVERAGE FITNESS", simulation.AverageFitness.ToString("0.000"), Text);
                DrawStatusValue(content.x + colWidth, content.y + 91f, colWidth, "CYCLE", simulation.EvaluationElapsed.ToString("0.0") + " / " + simulation.GenerationDuration.ToString("0.0") + " s", Text);
            }
            else
            {
                DrawStatusValue(content.x, content.y + 91f, colWidth, "AVERAGE FITNESS", simulation.AverageFitness.ToString("0.000"), Text);
                DrawStatusValue(content.x + colWidth, content.y + 91f, colWidth, "CYCLE", simulation.EvaluationElapsed.ToString("0.0") + " / " + simulation.GenerationDuration.ToString("0.0") + " s", Text);
                DrawStatusValue(content.x + colWidth * 2f, content.y + 91f, colWidth, "RESOURCES", simulation.AvailableResourceCount + " / " + simulation.ResourceCount, Text);
            }
            float footerY = content.y + 132f;
            GUI.Label(new Rect(content.x, footerY, content.width, 18f), "Births / deaths  " + simulation.BirthsThisCycle + " / " + simulation.DeathsThisCycle + "    Encounters  " + simulation.InteractionsThisCycle + "    Kills  " + simulation.PredationsThisCycle, smallStyle);
            GUI.Label(new Rect(content.x, footerY + 19f, content.width, 18f), "Energy avg  " + simulation.AverageEnergy.ToString("0.0") + "    Resources  " + simulation.AvailableResourceCount + " / " + simulation.ResourceCount + "    Features  " + simulation.EnvironmentFeatureCount, smallStyle);
            int lineageCount = simulation.LineageSummaries == null ? 0 : simulation.LineageSummaries.Count;
            int speciesCount = simulation.SpeciesSummaries == null ? 0 : simulation.SpeciesSummaries.Count;
            GUI.Label(new Rect(content.x, footerY + 38f, content.width, 18f), "Lineages  " + lineageCount + "    Extinct  " + simulation.ExtinctLineageCount + "    Morphotypes  " + speciesCount, smallStyle);
            GUI.Label(new Rect(content.x, footerY + 57f, content.width, 24f), simulation.EcologyStatus, wrapStyle);
        }

        private void DrawStatusValue(float x, float y, float width, string title, string value, Color color)
        {
            GUI.Label(new Rect(x, y, width - 8f, 14f), title, mutedStyle);
            valueStyle.normal.textColor = color;
            GUI.Label(new Rect(x, y + 14f, width - 8f, 24f), value, valueStyle);
        }

        private void DrawHistory()
        {
            GUI.Box(historyRect, GUIContent.none, panelStyle);
            Rect titleRect = new Rect(historyRect.x + 14f, historyRect.y + 10f, historyRect.width - 28f, 28f);
            if (SectionToggle(titleRect, "EVOLUTION HISTORY", ref historyPanelExpanded, "cycles, fitness, and natural-history events")) return;
            IReadOnlyList<GenerationRecord> records = simulation.GenerationHistory;
            int count = records == null ? 0 : records.Count;
            GUI.Label(new Rect(historyRect.x + 16f, historyRect.y + 43f, historyRect.width - 32f, 18f), "Completed cycles  " + count, smallStyle);
            if (count == 0)
            {
                GUI.Label(new Rect(historyRect.x + 16f, historyRect.y + 72f, historyRect.width - 32f, 40f), "The first generation is still being evaluated.", wrapStyle);
                return;
            }
            if (historyRect.height < 150f)
            {
                GenerationRecord latest = records[count - 1];
                GUI.Label(new Rect(historyRect.x + 16f, historyRect.y + 72f, historyRect.width - 32f, 38f), "Latest  G" + latest.generation + "   Best " + latest.bestFitness.ToString("0.000") + "   Avg " + latest.averageFitness.ToString("0.000"), wrapStyle);
                DrawLearningHistoryLine(latest.learning, historyRect.y + 108f);
                return;
            }
            Rect graphRect = new Rect(historyRect.x + 16f, historyRect.y + 67f, historyRect.width - 32f, historyRect.height - 108f);
            GUI.Box(graphRect, GUIContent.none, graphStyle);
            if (Event.current.type == EventType.Repaint) DrawFitnessGraph(graphRect, records);
            GenerationRecord latestRecord = records[count - 1];
            GUI.Label(new Rect(historyRect.x + 16f, historyRect.yMax - 35f, historyRect.width - 32f, 18f), "Best " + latestRecord.bestFitness.ToString("0.000") + "   Average " + latestRecord.averageFitness.ToString("0.000") + "   Kills " + latestRecord.predations, smallStyle);
            DrawLearningHistoryLine(latestRecord.learning, historyRect.yMax - 74f);
            IReadOnlyList<EvolutionEventRecord> events = simulation.EvolutionEvents;
            if (events != null && events.Count > 0)
            {
                EvolutionEventRecord latestEvent = events[events.Count - 1];
                GUI.Label(new Rect(historyRect.x + 16f, historyRect.yMax - 55f, historyRect.width - 32f, 18f), "Latest  " + latestEvent.type + " — " + latestEvent.message, smallStyle);
            }
        }

        private void DrawLearningHistoryLine(LearningTelemetrySummary learning, float y)
        {
            if (learning == null || learning.observedCount <= 0)
            {
                GUI.Label(new Rect(historyRect.x + 16f, y, historyRect.width - 32f, 18f), "Learning history  no runtime samples recorded", mutedStyle);
                return;
            }

            GUI.Label(
                new Rect(historyRect.x + 16f, y, historyRect.width - 32f, 18f),
                "Learning  enabled " + (learning.enabledRate * 100f).ToString("0") + "%  avg adapt "
                + learning.averageAdaptation.ToString("0.000") + "  max "
                + learning.maximumAdaptation.ToString("0.000") + "  signal "
                + learning.averageSignal.ToString("+0.000;-0.000;0.000")
                + "  (n=" + learning.observedCount + ")",
                smallStyle);
        }

        private void DrawControls()
        {
            GUI.Box(controlsRect, GUIContent.none, panelStyle);
            float footerHeight = 48f;
            Rect viewport = new Rect(controlsRect.x + 8f, controlsRect.y + 8f, controlsRect.width - 16f, Mathf.Max(70f, controlsRect.height - footerHeight - 16f));
            float contentWidth = viewport.width - 18f;
            float contentHeight = 1480f;
            controlsScrollPosition = GUI.BeginScrollView(viewport, controlsScrollPosition, new Rect(0f, 0f, contentWidth, contentHeight));
            GUILayout.BeginArea(new Rect(8f, 8f, contentWidth - 16f, contentHeight - 16f));
            GUILayout.Label("OBSERVATION", headerStyle);
            GUILayout.Space(6f);
            if (GUILayout.Button(simulation.IsPaused ? "Resume simulation" : "Pause simulation", simulation.IsPaused ? selectedButtonStyle : buttonStyle, GUILayout.Height(32f))) simulation.TogglePause();
            GUILayout.Space(5f);
            GUILayout.Label("TIME", sectionStyle);
            GUILayout.Label("Speed  " + simulation.SpeedLabel, smallStyle);
            GUILayout.BeginHorizontal(); DrawSpeedButton("x1", 1f); DrawSpeedButton("x10", 10f); DrawSpeedButton("x100", 100f); GUILayout.EndHorizontal();
            if (GUILayout.Button(
                    "World rendering  " + (simulation.RenderingEnabled ? "ON" : "OFF"),
                    simulation.RenderingEnabled ? selectedButtonStyle : buttonStyle,
                    GUILayout.Height(27f)))
            {
                simulation.ToggleWorldRendering();
            }
            GUILayout.Label(simulation.RenderingEnabled ? "Visible observation mode" : "Rendering disabled for faster simulation", mutedStyle);
            GUILayout.Label("Generation skip", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1", buttonStyle, GUILayout.Height(27f))) simulation.RequestGenerationSkip(1);
            if (GUILayout.Button("+10", buttonStyle, GUILayout.Height(27f))) simulation.RequestGenerationSkip(10);
            if (GUILayout.Button("+100", buttonStyle, GUILayout.Height(27f))) simulation.RequestGenerationSkip(100);
            GUILayout.EndHorizontal();
            GUILayout.Label("Queued  " + simulation.PendingGenerationSkips, smallStyle);
            GUILayout.Space(8f);
            if (!SectionHeader("ECOLOGY", ref ecologyExpanded, "population, interaction, and natural history")) DrawEcologyControls();
            GUILayout.Space(8f);
            if (!SectionHeader("HISTORY", ref historyExpanded, "ancestry and archives")) DrawHistoryControls();
            GUILayout.Space(8f);
            if (!SectionHeader("TUNING", ref tuningExpanded, "physics and life-cycle parameters")) DrawTuningControls();
            GUILayout.Space(8f);
            if (!SectionHeader("CAMERA", ref cameraExpanded, "navigation shortcuts"))
            {
                GUILayout.Label("WASD move / Q,E vertical / RMB look / Wheel dolly", smallStyle);
                if (GUILayout.Button("Reset camera view", buttonStyle, GUILayout.Height(26f))) simulation.ResetCameraView();
            }
            GUILayout.EndArea();
            GUI.EndScrollView();
            float footerY = controlsRect.yMax - footerHeight + 4f;
            float footerWidth = (controlsRect.width - 42f) * 0.5f;
            if (GUI.Button(new Rect(controlsRect.x + 16f, footerY, footerWidth, 28f), "Reset view", buttonStyle)) simulation.ResetCameraView();
            if (GUI.Button(new Rect(controlsRect.x + 26f + footerWidth, footerY, footerWidth, 28f), "Reset experiment", warningButtonStyle)) simulation.ResetSimulation();
        }

        private void DrawEcologyControls()
        {
            GUILayout.Label("Cycle interval  " + simulation.GenerationDuration.ToString("0.0") + " s", labelStyle);
            DrawPair("-5 s", "30 s", () => simulation.AdjustGenerationDuration(-5f), () => simulation.SetGenerationDuration(30f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+5 s", buttonStyle, GUILayout.Height(25f))) simulation.AdjustGenerationDuration(5f);
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            if (GUILayout.Button("Interactions  " + (simulation.EcologicalInteractionsEnabled ? "ON" : "OFF"), simulation.EcologicalInteractionsEnabled ? selectedButtonStyle : buttonStyle, GUILayout.Height(26f))) simulation.ToggleEcologicalInteractions();
            if (GUILayout.Button("Body collision isolation  " + (simulation.InterCreaturePhysicsIsolationEnabled ? "ON" : "OFF"), simulation.InterCreaturePhysicsIsolationEnabled ? selectedButtonStyle : buttonStyle, GUILayout.Height(26f))) simulation.ToggleInterCreaturePhysicsIsolation();
            GUILayout.Label("Transfer / damage  " + simulation.InteractionDamageMultiplier.ToString("0.0") + "x", labelStyle);
            DrawPair("-0.25", "+0.25", () => simulation.AdjustInteractionDamageMultiplier(-0.25f), () => simulation.AdjustInteractionDamageMultiplier(0.25f));
            IReadOnlyList<LineageSummary> lineages = simulation.LineageSummaries;
            IReadOnlyList<SpeciesSummary> species = simulation.SpeciesSummaries;
            GUILayout.Label("Lineages " + (lineages == null ? 0 : lineages.Count) + " / extinct " + simulation.ExtinctLineageCount + " / morphotypes " + (species == null ? 0 : species.Count), smallStyle);
            if (lineages != null)
            {
                int extinctShown = 0;
                for (int i = 0; i < lineages.Count && extinctShown < 3; i++)
                {
                    LineageSummary lineage = lineages[i];
                    if (lineage == null || !lineage.extinct) continue;
                    GUILayout.BeginHorizontal(); GUILayout.Label("Extinct G" + lineage.earliestGeneration + "–G" + lineage.latestGeneration + " (" + lineage.memberCount + ")", smallStyle);
                    if (GUILayout.Button("View", buttonStyle, GUILayout.Width(52f), GUILayout.Height(22f))) simulation.PreviewHistoryGenome(lineage.representativeGenomeId);
                    GUILayout.EndHorizontal(); extinctShown++;
                }
            }
            IReadOnlyList<EvolutionEventRecord> events = simulation.EvolutionEvents;
            if (events != null && events.Count > 0)
            {
                GUILayout.Label("Recent events", smallStyle);
                for (int i = Mathf.Max(0, events.Count - 3); i < events.Count; i++)
                {
                    EvolutionEventRecord record = events[i];
                    if (record != null) GUILayout.Label("G" + record.generation + " " + record.type + ": " + record.message, wrapStyle);
                }
            }
        }

        private void DrawHistoryControls()
        {
            IReadOnlyList<IndividualHistoryRecord> ancestry = simulation.SelectedAncestry;
            if (ancestry != null && ancestry.Count > 0)
            {
                IndividualHistoryRecord currentRecord = ancestry[simulation.AncestryCursor];
                GUILayout.Label("Viewing G" + currentRecord.generation + " / " + ancestry.Count, smallStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", buttonStyle, GUILayout.Height(24f))) simulation.StepAncestry(-1);
                if (GUILayout.Button("Preview", selectedButtonStyle, GUILayout.Height(24f))) simulation.PreviewSelectedAncestry();
                if (GUILayout.Button(">", buttonStyle, GUILayout.Height(24f))) simulation.StepAncestry(1);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Clear preview", buttonStyle, GUILayout.Height(24f))) simulation.ClearHistoryPreview();
            }
            else GUILayout.Label("Select an individual to browse its ancestry.", smallStyle);
            if (!SectionHeader("ARCHIVE", ref archiveExpanded, "save and restore"))
            {
                DrawPair("Save", "Load", simulation.SaveHistoryArchive, simulation.LoadHistoryArchive);
                DrawPair("Save world", "Load world", simulation.SaveWorldSnapshot, simulation.LoadWorldSnapshot);
                if (!string.IsNullOrEmpty(simulation.HistoryStatus)) GUILayout.Label(simulation.HistoryStatus, wrapStyle);
            }
        }

        private void DrawTuningControls()
        {
            GUILayout.Label("LIFE & REPRODUCTION", sectionStyle);
            GUILayout.Label("Metabolism  " + simulation.MetabolismPerSecond.ToString("0.00") + " / s", labelStyle);
            DrawPair("Met -0.05", "Met +0.05", () => simulation.AdjustMetabolism(-0.05f), () => simulation.AdjustMetabolism(0.05f));
            GUILayout.Label("Reproduction  " + simulation.ReproductionEnergyThreshold.ToString("0") + " energy", labelStyle);
            DrawPair("Threshold -5", "Threshold +5", () => simulation.AdjustReproductionEnergyThreshold(-5f), () => simulation.AdjustReproductionEnergyThreshold(5f));
            GUILayout.Label("Maximum age  " + simulation.MaxAgeSeconds.ToString("0") + " s", labelStyle);
            DrawPair("Age -10", "Age +10", () => simulation.AdjustMaxAge(-10f), () => simulation.AdjustMaxAge(10f));
            GUILayout.Label("JOINT PHYSICS", sectionStyle);
            GUILayout.Label("Drive force  " + simulation.JointDriveForce.ToString("0"), labelStyle);
            DrawPair("Force -25", "Force +25", () => simulation.AdjustJointDriveForce(-25f), () => simulation.AdjustJointDriveForce(25f));
            GUILayout.Label("Target speed  " + simulation.JointTargetSpeedDegrees.ToString("0") + " deg/s", labelStyle);
            DrawPair("Speed -40", "Speed +40", () => simulation.AdjustJointTargetSpeedDegrees(-40f), () => simulation.AdjustJointTargetSpeedDegrees(40f));
            GUILayout.Label("Damping  " + simulation.JointDamping.ToString("0.0"), labelStyle);
            DrawPair("Damp -2", "Damp +2", () => simulation.AdjustJointDamping(-2f), () => simulation.AdjustJointDamping(2f));
            GUILayout.Label("Settling  " + simulation.SettlingDuration.ToString("0.00") + " s", smallStyle);
            DrawPair("Settle -0.1", "Settle +0.1", () => simulation.AdjustSettlingDuration(-0.1f), () => simulation.AdjustSettlingDuration(0.1f));
        }

        private void DrawPair(string left, string right, System.Action leftAction, System.Action rightAction)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(left, buttonStyle, GUILayout.Height(24f))) leftAction();
            if (GUILayout.Button(right, buttonStyle, GUILayout.Height(24f))) rightAction();
            GUILayout.EndHorizontal();
        }

        private bool SectionHeader(string title, ref bool expanded, string hint)
        {
            if (GUILayout.Button((expanded ? "▾ " : "▸ ") + title, expanded ? selectedButtonStyle : buttonStyle, GUILayout.Height(27f))) expanded = !expanded;
            GUILayout.Label(hint, mutedStyle);
            return !expanded;
        }

        private bool SectionToggle(Rect rect, string title, ref bool expanded, string hint)
        {
            if (GUI.Button(rect, (expanded ? "▾ " : "▸ ") + title, expanded ? selectedButtonStyle : buttonStyle)) expanded = !expanded;
            GUI.Label(new Rect(rect.x, rect.yMax + 1f, rect.width, 16f), hint, mutedStyle);
            return !expanded;
        }

        private void DrawSelectedCreature()
        {
            GUI.Box(selectedRect, GUIContent.none, selectedCreature != null ? emphasisPanelStyle : panelStyle);
            if (selectedCreature != null && GUI.Button(new Rect(selectedRect.xMax - 102f, selectedRect.y + 10f, 88f, 27f), simulation.IsFollowingSelected ? "Following" : "Follow", simulation.IsFollowingSelected ? selectedButtonStyle : buttonStyle)) simulation.ToggleFollowSelected();
            if (GUI.Button(new Rect(selectedRect.x + 14f, selectedRect.y + 10f, selectedRect.width - 128f, 28f), (selectionExpanded ? "▾ " : "▸ ") + "SELECTED CREATURE", selectionExpanded ? selectedButtonStyle : buttonStyle)) selectionExpanded = !selectionExpanded;
            GUI.Label(new Rect(selectedRect.x + 16f, selectedRect.y + 42f, selectedRect.width - 32f, 18f), selectedCreature == null ? "No individual selected" : (simulation.IsFollowingSelected ? "TRACKING ACTIVE  ·  " : "SELECTED  ·  ") + selectedCreature.EcologicalTendency, selectedCreature != null ? SuccessLabel() : mutedStyle);
            if (!selectionExpanded) return;
            Rect viewport = new Rect(selectedRect.x + 8f, selectedRect.y + 65f, selectedRect.width - 16f, selectedRect.height - 73f);
            selectedScrollPosition = GUI.BeginScrollView(viewport, selectedScrollPosition, new Rect(0f, 0f, viewport.width - 18f, 420f));
            GUILayout.BeginArea(new Rect(8f, 8f, viewport.width - 34f, 390f));
            if (selectedCreature == null || selectedCreature.Genome == null)
            {
                GUILayout.Label("Click a body part to inspect an individual.", smallStyle); GUILayout.Label("Selection details and ancestry will appear here.", mutedStyle);
                GUILayout.EndArea(); GUI.EndScrollView(); return;
            }
            CreatureGenome genome = selectedCreature.Genome;
            GUILayout.Label("Genome ID  " + genome.genomeId, labelStyle);
            GUILayout.BeginHorizontal(); GUILayout.Label("Fitness  " + selectedCreature.SurvivalFitness.ToString("0.000"), labelStyle); GUILayout.Label("Parts  " + selectedCreature.BodyPartCount, labelStyle); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Joints  " + selectedCreature.JointCount, labelStyle); GUILayout.Label("Generation  " + genome.generation, labelStyle); GUILayout.EndHorizontal();
            GUILayout.Label("Parent ID  " + (string.IsNullOrEmpty(genome.parentId) ? "Founder" : genome.parentId), smallStyle);
            GUILayout.Label("Second parent  " + (string.IsNullOrEmpty(genome.secondaryParentId) ? "Asexual / none" : genome.secondaryParentId), smallStyle);
            GUILayout.BeginHorizontal(); GUILayout.Label("Energy  " + selectedCreature.Energy.ToString("0.0") + " / " + selectedCreature.MaxEnergy.ToString("0.0"), smallStyle); GUILayout.Label("Distance  " + selectedCreature.CurrentDistance.ToString("0.000"), smallStyle); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Age  " + selectedCreature.AgeSeconds.ToString("0.0") + " s", smallStyle); GUILayout.Label("Offspring  " + selectedCreature.OffspringCount, smallStyle); GUILayout.EndHorizontal();
            GUILayout.Label("Status  " + (selectedCreature.IsAlive ? "Alive" : selectedCreature.DeathReason), selectedCreature.IsAlive ? smallStyle : warningButtonStyle);
            GUILayout.Label("Recorded descendants  " + simulation.SelectedDescendantCount, smallStyle);
            EcologyGene ecology = genome.ecology;
            if (ecology != null)
            {
                GUILayout.Label("Tendency  " + selectedCreature.EcologicalTendency, smallStyle);
                GUILayout.Label("Traits  forage " + ecology.foragingDrive.ToString("0.00") + "  interact " + ecology.predationDrive.ToString("0.00") + "  defend " + ecology.defenseDrive.ToString("0.00"), smallStyle);
                GUILayout.Label("Sensor  " + ecology.sensorRange.ToString("0.0") + "  efficiency " + ecology.energyEfficiency.ToString("0.00"), smallStyle);
                GUILayout.Label("Brain  intent " + selectedCreature.InteractionIntent.ToString("0.00") + "  social " + selectedCreature.SocialIntent.ToString("0.00") + "  kills " + selectedCreature.KillCount, smallStyle);
            }
            LifetimeLearningGene learning = genome.brain == null ? null : genome.brain.learning;
            if (learning != null)
            {
                string learningState = selectedCreature.LifetimeLearningEnabled ? "ACTIVE" : "DISABLED";
                GUILayout.Label(
                    "Lifetime learning  " + learningState
                    + "  rate " + learning.learningRate.ToString("0.000"),
                    selectedCreature.LifetimeLearningEnabled ? SuccessLabel() : mutedStyle);
                GUILayout.Label(
                    "Learning signal  " + selectedCreature.LearningSignal.ToString("+0.000;-0.000;0.000")
                    + "  adaptation " + selectedCreature.LearningAdaptationMagnitude.ToString("0.000"),
                    smallStyle);
            }
            GUILayout.Space(4f); GUILayout.Label("ANCESTRY (newest to oldest)", sectionStyle); GUILayout.Label(BuildAncestryLabel(), wrapStyle);
            GUILayout.EndArea(); GUI.EndScrollView();
        }

        private GUIStyle SuccessLabel()
        {
            valueStyle.normal.textColor = Success;
            return valueStyle;
        }

        private string BuildAncestryLabel()
        {
            IReadOnlyList<IndividualHistoryRecord> ancestry = simulation.SelectedAncestry;
            if (ancestry == null || ancestry.Count == 0) return "Not recorded yet.";
            string label = string.Empty;
            for (int i = 0; i < ancestry.Count; i++) label += (i > 0 ? "  >  " : string.Empty) + "G" + ancestry[i].generation;
            return label;
        }

        private void DrawSpeedButton(string label, float speed)
        {
            if (GUILayout.Button(label, Mathf.Approximately(simulation.SimulationSpeed, speed) ? selectedButtonStyle : buttonStyle, GUILayout.Height(28f))) simulation.SetSimulationSpeed(speed);
        }

        private void DrawFitnessGraph(Rect graphRect, IReadOnlyList<GenerationRecord> records)
        {
            int count = records.Count, start = Mathf.Max(0, count - 160), last = count - 1;
            float maximum = 0.001f;
            for (int i = start; i < count; i++) maximum = Mathf.Max(maximum, records[i].bestFitness, records[i].averageFitness);
            DrawGraphLine(new Vector2(graphRect.x, graphRect.y + graphRect.height * 0.5f), new Vector2(graphRect.xMax, graphRect.y + graphRect.height * 0.5f), new Color(0.30f, 0.38f, 0.40f, 0.35f));
            for (int i = start + 1; i < count; i++)
            {
                Vector2 pb = GraphPoint(graphRect, i - 1, start, last, records[i - 1].bestFitness, maximum), cb = GraphPoint(graphRect, i, start, last, records[i].bestFitness, maximum);
                Vector2 pa = GraphPoint(graphRect, i - 1, start, last, records[i - 1].averageFitness, maximum), ca = GraphPoint(graphRect, i, start, last, records[i].averageFitness, maximum);
                DrawGraphLine(pb, cb, Accent); DrawGraphLine(pa, ca, new Color(0.57f, 0.65f, 0.67f, 0.9f));
            }
        }

        private static Vector2 GraphPoint(Rect rect, int index, int first, int last, float value, float maximum)
        {
            float x = rect.x + Mathf.InverseLerp(first, Mathf.Max(first + 1, last), index) * rect.width;
            return new Vector2(x, rect.yMax - Mathf.Clamp01(value / maximum) * rect.height);
        }

        private void DrawGraphLine(Vector2 from, Vector2 to, Color color)
        {
            if (graphLineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                graphLineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                graphLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); graphLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); graphLineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); graphLineMaterial.SetInt("_ZWrite", 0);
            }
            GL.PushMatrix(); graphLineMaterial.SetPass(0); GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f); GL.Begin(GL.LINES); GL.Color(color); GL.Vertex3(from.x, from.y, 0f); GL.Vertex3(to.x, to.y, 0f); GL.End(); GL.PopMatrix();
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelStyle = MakeStyle(GUI.skin.box, new Color(0.055f, 0.065f, 0.068f, 0.97f)); emphasisPanelStyle = MakeStyle(panelStyle, new Color(0.06f, 0.085f, 0.088f, 0.98f));
            headerStyle = MakeTextStyle(17, FontStyle.Bold, new Color(0.88f, 0.94f, 0.94f, 1f)); sectionStyle = MakeTextStyle(12, FontStyle.Bold, Accent); labelStyle = MakeTextStyle(14, FontStyle.Normal, Text); valueStyle = MakeTextStyle(18, FontStyle.Bold, Text); smallStyle = MakeTextStyle(12, FontStyle.Normal, TextMuted); mutedStyle = MakeTextStyle(11, FontStyle.Normal, new Color(0.43f, 0.51f, 0.53f, 1f));
            buttonStyle = MakeButtonStyle(new Color(0.10f, 0.12f, 0.13f, 1f), Text); selectedButtonStyle = MakeButtonStyle(AccentDim, Color.white); warningButtonStyle = MakeButtonStyle(new Color(0.28f, 0.16f, 0.10f, 1f), Warning); graphStyle = MakeStyle(GUI.skin.box, new Color(0.035f, 0.043f, 0.045f, 0.98f)); wrapStyle = new GUIStyle(smallStyle) { wordWrap = true };
        }

        private static GUIStyle MakeTextStyle(int size, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, wordWrap = false }; style.normal.textColor = color; style.hover.textColor = color; return style;
        }

        private static GUIStyle MakeButtonStyle(Color background, Color textColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(8, 8, 5, 5), margin = new RectOffset(2, 2, 2, 2) }; style.normal.background = MakeTexture(background); style.normal.textColor = textColor; style.hover.background = MakeTexture(Color.Lerp(background, Accent, 0.22f)); style.hover.textColor = Color.white; style.active.background = MakeTexture(Color.Lerp(background, Color.black, 0.18f)); style.active.textColor = Color.white; style.focused.background = style.hover.background; style.focused.textColor = Color.white; return style;
        }

        private static GUIStyle MakeStyle(GUIStyle source, Color background)
        {
            GUIStyle style = new GUIStyle(source); style.normal.background = MakeTexture(background); return style;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        private void OnDestroy()
        {
            DestroyStyleTextures(panelStyle); DestroyStyleTextures(emphasisPanelStyle); DestroyStyleTextures(selectedButtonStyle); DestroyStyleTextures(warningButtonStyle); DestroyStyleTextures(buttonStyle); DestroyStyleTextures(graphStyle);
            if (graphLineMaterial != null) Destroy(graphLineMaterial);
        }

        private static void DestroyStyleTextures(GUIStyle style)
        {
            if (style == null) return;
            DestroyTexture(style.normal.background); DestroyTexture(style.hover.background); DestroyTexture(style.active.background);
        }

        private static void DestroyTexture(Texture texture)
        {
            if (texture != null) Destroy(texture);
        }
    }
}
