using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Small runtime presentation layer for the Prototype 2 ecology loop.
    /// IMGUI keeps the blank template scene free from serialized UI assets.
    /// </summary>
    public sealed class EvolutionLabUI : MonoBehaviour
    {
        private EvolutionSimulation simulation;
        private Creature selectedCreature;
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle graphStyle;
        private GUIStyle wrapStyle;
        private Material graphLineMaterial;
        private Vector2 controlsScrollPosition;
        private Rect statsRect;
        private Rect controlsRect;
        private Rect historyRect;
        private Rect selectedRect;

        public void Bind(EvolutionSimulation owner)
        {
            simulation = owner;
        }

        public void SetSelectedCreature(Creature creature)
        {
            selectedCreature = creature;
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return statsRect.Contains(guiPosition)
                || controlsRect.Contains(guiPosition)
                || historyRect.Contains(guiPosition)
                || selectedRect.Contains(guiPosition);
        }

        private void OnGUI()
        {
            if (simulation == null)
            {
                return;
            }

            EnsureStyles();
            float width = Mathf.Max(960f, Screen.width);
            float height = Mathf.Max(540f, Screen.height);
            statsRect = new Rect(18f, 18f, 380f, 270f);
            controlsRect = new Rect(width - 368f, 18f, 350f, height - 36f);
            float selectedHeight = Mathf.Clamp(height * 0.36f, 300f, 380f);
            selectedRect = new Rect(18f, height - selectedHeight - 18f, 380f, selectedHeight);
            float historyWidth = Mathf.Max(220f, controlsRect.x - statsRect.xMax - 24f);
            historyRect = new Rect(statsRect.xMax + 12f, statsRect.y, historyWidth, statsRect.height);

            DrawStatistics();
            DrawHistory();
            DrawControls();
            DrawSelectedCreature();
        }

        private void DrawStatistics()
        {
            GUI.Box(statsRect, GUIContent.none, panelStyle);
            Rect content = new Rect(statsRect.x + 16f, statsRect.y + 12f, statsRect.width - 32f, statsRect.height - 24f);
            GUI.Label(new Rect(content.x, content.y, content.width, 22f), "EVOLUTION LAB", headerStyle);
            GUI.Label(new Rect(content.x, content.y + 22f, content.width, 16f), "MORPHOLOGICAL ECOLOGY", smallStyle);
            GUI.Label(new Rect(content.x, content.y + 42f, content.width, 18f), "Cycle          " + simulation.Generation, labelStyle);
            GUI.Label(
                new Rect(content.x, content.y + 60f, content.width, 18f),
                "Population     " + simulation.PopulationCount + " / " + simulation.CarryingCapacity,
                labelStyle);
            GUI.Label(new Rect(content.x, content.y + 78f, content.width, 18f), "Best survival  " + simulation.BestFitness.ToString("0.000"), labelStyle);
            GUI.Label(new Rect(content.x, content.y + 96f, content.width, 18f), "Average survival " + simulation.AverageFitness.ToString("0.000"), labelStyle);
            GUI.Label(
                new Rect(content.x, content.y + 114f, content.width, 16f),
                "Births / deaths " + simulation.BirthsThisCycle + " / " + simulation.DeathsThisCycle,
                smallStyle);
            GUI.Label(new Rect(content.x, content.y + 130f, content.width, 16f), "Average energy " + simulation.AverageEnergy.ToString("0.0"), smallStyle);
            GUI.Label(
                new Rect(content.x, content.y + 146f, content.width, 16f),
                "Resources      " + simulation.AvailableResourceCount + " / " + simulation.ResourceCount,
                smallStyle);
            GUI.Label(
                new Rect(content.x, content.y + 162f, content.width, 16f),
                "Cycle time     " + simulation.EvaluationElapsed.ToString("0.0") + " / " + simulation.GenerationDuration.ToString("0.0") + " s",
                smallStyle);
            GUI.Label(
                new Rect(content.x, content.y + 178f, content.width, 16f),
                "Encounters     " + simulation.InteractionsThisCycle + " / kills " + simulation.PredationsThisCycle,
                smallStyle);
            GUI.Label(
                new Rect(content.x, content.y + 194f, content.width, 16f),
                "Lineages       " + (simulation.LineageSummaries == null ? 0 : simulation.LineageSummaries.Count)
                + " / extinct " + simulation.ExtinctLineageCount
                + " / species " + (simulation.SpeciesSummaries == null ? 0 : simulation.SpeciesSummaries.Count),
                smallStyle);
            GUI.Label(
                new Rect(content.x, content.y + 210f, content.width, 16f),
                "World features  " + simulation.EnvironmentFeatureCount,
                smallStyle);
            GUI.Label(new Rect(content.x, content.y + 226f, content.width, 30f), simulation.EcologyStatus, wrapStyle);
        }

        private void DrawHistory()
        {
            if (historyRect.height < 42f)
            {
                return;
            }

            GUI.Box(historyRect, GUIContent.none, panelStyle);
            GUI.Label(
                new Rect(historyRect.x + 16f, historyRect.y + 10f, historyRect.width - 32f, 24f),
                "EVOLUTION HISTORY",
                headerStyle);

            IReadOnlyList<GenerationRecord> records = simulation.GenerationHistory;
            int recordCount = records == null ? 0 : records.Count;
            GUI.Label(
                new Rect(historyRect.x + 16f, historyRect.y + 31f, historyRect.width - 32f, 18f),
                "Completed ecology cycles  " + recordCount,
                smallStyle);

            if (recordCount == 0)
            {
                GUI.Label(
                    new Rect(historyRect.x + 16f, historyRect.y + 58f, historyRect.width - 32f, 40f),
                    "The first generation is still being evaluated.",
                    smallStyle);
                return;
            }

            if (historyRect.height < 150f)
            {
                GenerationRecord latest = records[recordCount - 1];
                GUI.Label(
                    new Rect(historyRect.x + 16f, historyRect.y + 58f, historyRect.width - 32f, 40f),
                    "Latest  G" + latest.generation + "   Best " + latest.bestFitness.ToString("0.000")
                    + "   Avg " + latest.averageFitness.ToString("0.000"),
                    wrapStyle);
                return;
            }

            Rect graphRect = new Rect(
                historyRect.x + 16f,
                historyRect.y + 54f,
                historyRect.width - 32f,
                historyRect.height - 86f);
            GUI.Box(graphRect, GUIContent.none, graphStyle);
            if (Event.current.type == EventType.Repaint)
            {
                DrawFitnessGraph(graphRect, records);
            }

            GenerationRecord latestRecord = records[recordCount - 1];
            GUI.Label(
                new Rect(historyRect.x + 16f, historyRect.yMax - 28f, historyRect.width - 32f, 18f),
                "Best " + latestRecord.bestFitness.ToString("0.000")
                + "   Average " + latestRecord.averageFitness.ToString("0.000")
                + "   Encounters " + latestRecord.interactions
                + "   Kills " + latestRecord.predations,
                smallStyle);

            IReadOnlyList<EvolutionEventRecord> events = simulation.EvolutionEvents;
            if (events != null && events.Count > 0)
            {
                EvolutionEventRecord latestEvent = events[events.Count - 1];
                GUI.Label(
                    new Rect(historyRect.x + 16f, historyRect.yMax - 46f, historyRect.width - 32f, 18f),
                    "Latest event  " + latestEvent.type + " — " + latestEvent.message,
                    smallStyle);
            }
        }

        private void DrawFitnessGraph(Rect graphRect, IReadOnlyList<GenerationRecord> records)
        {
            int count = records.Count;
            int start = Mathf.Max(0, count - 160);
            int last = count - 1;
            float maximum = 0.001f;
            for (int i = start; i < count; i++)
            {
                maximum = Mathf.Max(maximum, records[i].bestFitness, records[i].averageFitness);
            }

            DrawGraphLine(
                new Vector2(graphRect.x, graphRect.y + graphRect.height * 0.5f),
                new Vector2(graphRect.xMax, graphRect.y + graphRect.height * 0.5f),
                new Color(0.28f, 0.45f, 0.52f, 0.35f));
            DrawGraphLine(
                new Vector2(graphRect.x, graphRect.yMax - 1f),
                new Vector2(graphRect.xMax, graphRect.yMax - 1f),
                new Color(0.28f, 0.45f, 0.52f, 0.5f));

            for (int i = start + 1; i < count; i++)
            {
                Vector2 previousBest = GraphPoint(graphRect, i - 1, start, last, records[i - 1].bestFitness, maximum);
                Vector2 currentBest = GraphPoint(graphRect, i, start, last, records[i].bestFitness, maximum);
                Vector2 previousAverage = GraphPoint(graphRect, i - 1, start, last, records[i - 1].averageFitness, maximum);
                Vector2 currentAverage = GraphPoint(graphRect, i, start, last, records[i].averageFitness, maximum);
                DrawGraphLine(previousBest, currentBest, new Color(0.35f, 0.9f, 1f, 1f));
                DrawGraphLine(previousAverage, currentAverage, new Color(0.55f, 0.68f, 0.76f, 0.9f));
            }
        }

        private static Vector2 GraphPoint(
            Rect graphRect,
            int index,
            int firstIndex,
            int lastIndex,
            float value,
            float maximum)
        {
            float x = graphRect.x + Mathf.InverseLerp(firstIndex, Mathf.Max(firstIndex + 1, lastIndex), index) * graphRect.width;
            float y = graphRect.yMax - Mathf.Clamp01(value / maximum) * graphRect.height;
            return new Vector2(x, y);
        }

        private void DrawGraphLine(Vector2 from, Vector2 to, Color color)
        {
            if (graphLineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    return;
                }

                graphLineMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                graphLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                graphLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                graphLineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                graphLineMaterial.SetInt("_ZWrite", 0);
            }

            GL.PushMatrix();
            graphLineMaterial.SetPass(0);
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(from.x, from.y, 0f);
            GL.Vertex3(to.x, to.y, 0f);
            GL.End();
            GL.PopMatrix();
        }

        private void DrawControls()
        {
            GUI.Box(controlsRect, GUIContent.none, panelStyle);
            Rect viewportRect = new Rect(
                controlsRect.x + 8f,
                controlsRect.y + 8f,
                controlsRect.width - 16f,
                controlsRect.height - 78f);
            Rect contentRect = new Rect(0f, 0f, viewportRect.width - 18f, 1020f);
            controlsScrollPosition = GUI.BeginScrollView(viewportRect, controlsScrollPosition, contentRect);
            GUILayout.BeginArea(new Rect(8f, 8f, contentRect.width - 16f, contentRect.height - 16f));
            GUILayout.Label("SIMULATION CONTROLS", headerStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button(simulation.IsPaused ? "Resume" : "Pause", buttonStyle, GUILayout.Height(32f)))
            {
                simulation.TogglePause();
            }

            GUILayout.Space(6f);
            GUILayout.Label("Simulation speed  " + simulation.SpeedLabel, smallStyle);
            GUILayout.BeginHorizontal();
            DrawSpeedButton("x1", 1f);
            DrawSpeedButton("x10", 10f);
            DrawSpeedButton("x100", 100f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Ecology cycle skip", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1", buttonStyle, GUILayout.Height(28f)))
            {
                simulation.RequestGenerationSkip(1);
            }

            if (GUILayout.Button("+10", buttonStyle, GUILayout.Height(28f)))
            {
                simulation.RequestGenerationSkip(10);
            }

            if (GUILayout.Button("+100", buttonStyle, GUILayout.Height(28f)))
            {
                simulation.RequestGenerationSkip(100);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Queued: " + simulation.PendingGenerationSkips, smallStyle);

            GUILayout.Space(6f);
            GUILayout.Label("ECOLOGY CYCLE", smallStyle);
            GUILayout.Label(
                "Cycle interval      " + simulation.GenerationDuration.ToString("0.0") + " s",
                labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-5 s", buttonStyle, GUILayout.Height(25f)))
            {
                simulation.AdjustGenerationDuration(-5f);
            }

            if (GUILayout.Button("30 s", buttonStyle, GUILayout.Height(25f)))
            {
                simulation.SetGenerationDuration(30f);
            }

            if (GUILayout.Button("+5 s", buttonStyle, GUILayout.Height(25f)))
            {
                simulation.AdjustGenerationDuration(5f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("LIFE & REPRODUCTION", smallStyle);
            GUILayout.Label(
                "Metabolism         " + simulation.MetabolismPerSecond.ToString("0.00") + " / s",
                labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Met -0.05", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustMetabolism(-0.05f);
            }

            if (GUILayout.Button("Met +0.05", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustMetabolism(0.05f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(
                "Reproduction      " + simulation.ReproductionEnergyThreshold.ToString("0") + " energy",
                labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Threshold -5", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustReproductionEnergyThreshold(-5f);
            }

            if (GUILayout.Button("Threshold +5", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustReproductionEnergyThreshold(5f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Maximum age       " + simulation.MaxAgeSeconds.ToString("0") + " s", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Age -10", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustMaxAge(-10f);
            }

            if (GUILayout.Button("Age +10", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustMaxAge(10f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("EMERGENT INTERACTIONS", smallStyle);
            if (GUILayout.Button(
                    "Interactions: " + (simulation.EcologicalInteractionsEnabled ? "ON" : "OFF"),
                    buttonStyle,
                    GUILayout.Height(25f)))
            {
                simulation.ToggleEcologicalInteractions();
            }

            if (GUILayout.Button(
                    "Body collision isolation: " + (simulation.InterCreaturePhysicsIsolationEnabled ? "ON" : "OFF"),
                    buttonStyle,
                    GUILayout.Height(25f)))
            {
                simulation.ToggleInterCreaturePhysicsIsolation();
            }

            GUILayout.Label(
                "Encounter energy transfer " + simulation.InteractionDamageMultiplier.ToString("0.0") + "x",
                labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Damage -0.25", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustInteractionDamageMultiplier(-0.25f);
            }

            if (GUILayout.Button("Damage +0.25", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustInteractionDamageMultiplier(0.25f);
            }

            GUILayout.EndHorizontal();
            IReadOnlyList<LineageSummary> lineages = simulation.LineageSummaries;
            IReadOnlyList<SpeciesSummary> species = simulation.SpeciesSummaries;
            GUILayout.Label(
                "Lineages " + (lineages == null ? 0 : lineages.Count)
                + " / extinct " + simulation.ExtinctLineageCount
                + " / morphotypes " + (species == null ? 0 : species.Count),
                smallStyle);

            if (lineages != null)
            {
                int extinctShown = 0;
                for (int i = 0; i < lineages.Count && extinctShown < 3; i++)
                {
                    LineageSummary lineage = lineages[i];
                    if (lineage == null || !lineage.extinct)
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(
                        "Extinct G" + lineage.earliestGeneration + "–G" + lineage.latestGeneration
                        + " (" + lineage.memberCount + ")",
                        smallStyle);
                    if (GUILayout.Button("View", buttonStyle, GUILayout.Width(52f), GUILayout.Height(22f)))
                    {
                        simulation.PreviewHistoryGenome(lineage.representativeGenomeId);
                    }

                    GUILayout.EndHorizontal();
                    extinctShown++;
                }
            }

            IReadOnlyList<EvolutionEventRecord> events = simulation.EvolutionEvents;
            if (events != null && events.Count > 0)
            {
                GUILayout.Label("RECENT NATURAL HISTORY", smallStyle);
                int eventStart = Mathf.Max(0, events.Count - 3);
                for (int i = eventStart; i < events.Count; i++)
                {
                    EvolutionEventRecord record = events[i];
                    if (record != null)
                    {
                        GUILayout.Label(
                            "G" + record.generation + " " + record.type + ": " + record.message,
                            wrapStyle);
                    }
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("JOINT PHYSICS", smallStyle);
            GUILayout.Label("Drive force       " + simulation.JointDriveForce.ToString("0"), labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force -25", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDriveForce(-25f);
            }

            if (GUILayout.Button("Force +25", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDriveForce(25f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Target speed     " + simulation.JointTargetSpeedDegrees.ToString("0") + " deg/s", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Speed -40", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointTargetSpeedDegrees(-40f);
            }

            if (GUILayout.Button("Speed +40", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointTargetSpeedDegrees(40f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Damping          " + simulation.JointDamping.ToString("0.0"), labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Damp -2", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDamping(-2f);
            }

            if (GUILayout.Button("Damp +2", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDamping(2f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Settling         " + simulation.SettlingDuration.ToString("0.00") + " s", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Settle -0.1", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustSettlingDuration(-0.1f);
            }

            if (GUILayout.Button("Settle +0.1", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustSettlingDuration(0.1f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("CAMERA", smallStyle);
            GUILayout.Label("WASD move / Q,E vertical / RMB look / Wheel dolly", smallStyle);

            GUILayout.Space(8f);
            GUILayout.Label("HISTORY BROWSER", smallStyle);
            IReadOnlyList<IndividualHistoryRecord> ancestry = simulation.SelectedAncestry;
            if (ancestry != null && ancestry.Count > 0)
            {
                IndividualHistoryRecord currentRecord = ancestry[simulation.AncestryCursor];
                GUILayout.Label(
                    "Viewing G" + currentRecord.generation + " / " + ancestry.Count,
                    smallStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", buttonStyle, GUILayout.Height(24f)))
                {
                    simulation.StepAncestry(-1);
                }

                if (GUILayout.Button("Preview", buttonStyle, GUILayout.Height(24f)))
                {
                    simulation.PreviewSelectedAncestry();
                }

                if (GUILayout.Button(">", buttonStyle, GUILayout.Height(24f)))
                {
                    simulation.StepAncestry(1);
                }

                GUILayout.EndHorizontal();
                if (GUILayout.Button("Clear preview", buttonStyle, GUILayout.Height(24f)))
                {
                    simulation.ClearHistoryPreview();
                }
            }
            else
            {
                GUILayout.Label("Select an individual to browse its ancestry.", smallStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label("HISTORY ARCHIVE", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.SaveHistoryArchive();
            }

            if (GUILayout.Button("Load", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.LoadHistoryArchive();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save world", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.SaveWorldSnapshot();
            }

            if (GUILayout.Button("Load world", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.LoadWorldSnapshot();
            }

            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(simulation.HistoryStatus))
            {
                GUILayout.Label(simulation.HistoryStatus, wrapStyle);
            }
            GUILayout.EndArea();
            GUI.EndScrollView();

            float footerY = controlsRect.y + controlsRect.height - 62f;
            float footerWidth = (controlsRect.width - 42f) * 0.5f;
            if (GUI.Button(
                    new Rect(controlsRect.x + 16f, footerY, footerWidth, 26f),
                    "Reset camera view",
                    buttonStyle))
            {
                simulation.ResetCameraView();
            }

            if (GUI.Button(
                    new Rect(controlsRect.x + 26f + footerWidth, footerY, footerWidth, 26f),
                    "Reset experiment",
                    buttonStyle))
            {
                simulation.ResetSimulation();
            }
        }

        private void DrawSelectedCreature()
        {
            GUI.Box(selectedRect, GUIContent.none, panelStyle);
            if (selectedCreature != null
                && GUI.Button(
                    new Rect(selectedRect.x + selectedRect.width - 98f, selectedRect.y + 12f, 82f, 24f),
                    simulation.IsFollowingSelected ? "Unfollow" : "Follow",
                    buttonStyle))
            {
                simulation.ToggleFollowSelected();
            }

            GUILayout.BeginArea(new Rect(selectedRect.x + 16f, selectedRect.y + 12f, selectedRect.width - 32f, selectedRect.height - 24f));
            GUILayout.Label("INDIVIDUAL OBS.", headerStyle);
            GUILayout.Space(4f);

            if (selectedCreature == null || selectedCreature.Genome == null)
            {
                GUILayout.Label("Click a body part to inspect an individual.", smallStyle);
                GUILayout.Label("The highlighted organism is selected for this generation.", smallStyle);
                GUILayout.EndArea();
                return;
            }

            CreatureGenome genome = selectedCreature.Genome;
            GUILayout.Label("Genome ID  " + genome.genomeId, labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Survival " + selectedCreature.SurvivalFitness.ToString("0.000"), labelStyle, GUILayout.Width(156f));
            GUILayout.Label("Parts " + selectedCreature.BodyPartCount, labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Joints " + selectedCreature.JointCount, labelStyle, GUILayout.Width(156f));
            GUILayout.Label("Generation " + genome.generation, labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label("Parent ID  " + (string.IsNullOrEmpty(genome.parentId) ? "Founder" : genome.parentId), smallStyle);
            GUILayout.Label(
                "Second parent  " + (string.IsNullOrEmpty(genome.secondaryParentId) ? "Asexual / none" : genome.secondaryParentId),
                smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "Energy " + selectedCreature.Energy.ToString("0.0") + " / " + selectedCreature.MaxEnergy.ToString("0.0"),
                smallStyle,
                GUILayout.Width(156f));
            GUILayout.Label("Distance " + selectedCreature.CurrentDistance.ToString("0.000"), smallStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Age " + selectedCreature.AgeSeconds.ToString("0.0") + " s", smallStyle, GUILayout.Width(156f));
            GUILayout.Label("Offspring " + selectedCreature.OffspringCount, smallStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "Status " + (selectedCreature.IsAlive ? "Alive" : selectedCreature.DeathReason),
                smallStyle);
            GUILayout.Label("Recorded descendants  " + simulation.SelectedDescendantCount, smallStyle);
            EcologyGene ecology = genome.ecology;
            if (ecology != null)
            {
                GUILayout.Label("Tendency  " + selectedCreature.EcologicalTendency, smallStyle);
                GUILayout.Label(
                    "Traits    forage " + ecology.foragingDrive.ToString("0.00")
                    + "  interact " + ecology.predationDrive.ToString("0.00")
                    + "  defend " + ecology.defenseDrive.ToString("0.00"),
                    smallStyle);
                GUILayout.Label(
                    "Sensor    " + ecology.sensorRange.ToString("0.0")
                    + "  efficiency " + ecology.energyEfficiency.ToString("0.00"),
                    smallStyle);
                GUILayout.Label(
                    "Brain     intent " + selectedCreature.InteractionIntent.ToString("0.00")
                    + "  social " + selectedCreature.SocialIntent.ToString("0.00")
                    + "  kills " + selectedCreature.KillCount,
                    smallStyle);
            }
            GUILayout.Space(4f);
            GUILayout.Label("ANCESTRY (newest to oldest)", smallStyle);
            GUILayout.Label(BuildAncestryLabel(), wrapStyle);
            GUILayout.EndArea();
        }

        private string BuildAncestryLabel()
        {
            IReadOnlyList<IndividualHistoryRecord> ancestry = simulation.SelectedAncestry;
            if (ancestry == null || ancestry.Count == 0)
            {
                return "Not recorded yet.";
            }

            string label = string.Empty;
            for (int i = 0; i < ancestry.Count; i++)
            {
                if (i > 0)
                {
                    label += "  >  ";
                }

                label += "G" + ancestry[i].generation;
            }

            return label;
        }

        private void DrawSpeedButton(string label, float speed)
        {
            GUIStyle style = Mathf.Approximately(simulation.SimulationSpeed, speed)
                ? selectedButtonStyle
                : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(28f)))
            {
                simulation.SetSimulationSpeed(speed);
            }
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 12, 12),
                normal = { background = MakeTexture(new Color(0.035f, 0.055f, 0.075f, 0.94f)) }
            };
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.94f, 1f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.86f, 0.91f, 0.94f, 1f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.58f, 0.72f, 0.78f, 1f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            selectedButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = MakeTexture(new Color(0.12f, 0.42f, 0.52f, 1f)), textColor = Color.white }
            };
            graphStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(new Color(0.02f, 0.035f, 0.05f, 0.92f)) }
            };
            wrapStyle = new GUIStyle(smallStyle)
            {
                wordWrap = true
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            DestroyStyleTexture(panelStyle);
            DestroyStyleTexture(selectedButtonStyle);
            DestroyStyleTexture(graphStyle);
            if (graphLineMaterial != null)
            {
                Destroy(graphLineMaterial);
            }
        }

        private static void DestroyStyleTexture(GUIStyle style)
        {
            if (style != null && style.normal != null && style.normal.background != null)
            {
                Destroy(style.normal.background);
            }
        }
    }
}
