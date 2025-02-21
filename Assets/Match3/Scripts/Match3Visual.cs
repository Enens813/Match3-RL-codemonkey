using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;

/*
 * Visual Representation of the underlying Match3 Grid
 * */
public class Match3Visual : MonoBehaviour {

    public event EventHandler OnStateChanged;
    public event EventHandler OnStateWaitingForUser;

    public enum State {
        Busy,
        WaitingForUser,
        TryFindMatches,
        GameOver,
    }

    [SerializeField] private Transform pfGemGridVisual;
    [SerializeField] private Transform pfGlassGridVisual;
    [SerializeField] private Transform pfBackgroundGridVisual;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Match3 match3;

    private Grid<Match3.GemGridPosition> grid;
    private Dictionary<Match3.GemGrid, GemGridVisual> gemGridDictionary;
    private Dictionary<Match3.GemGridPosition, GlassGridVisual> glassGridDictionary;

    private bool isSetup;
    private State state;
    private float busyTimer;
    private Action onBusyTimerElapsedAction;

    private int startDragX;
    private int startDragY;
    private Vector3 startDragMouseWorldPosition;

    private void Awake() {
        Debug.Log("match3Visual setting...");
        isSetup = false;
        
        gemGridDictionary = new Dictionary<Match3.GemGrid, GemGridVisual>();
        glassGridDictionary = new Dictionary<Match3.GemGridPosition, GlassGridVisual>();

        match3 = FindFirstObjectByType<Match3>(); // Ensure match3 is assigned after reload
        if (match3 == null) {
            Debug.LogError("Match3 not found on reload!");
            return;
        }
        
        match3.OnLevelSet += Match3_OnLevelSet;

        state = State.Busy;  // Reset state to initial
        SetBusyState(0.5f, () => SetState(State.TryFindMatches)); 
    }

    private void Match3_OnLevelSet(object sender, Match3.OnLevelSetEventArgs e) {
        FunctionTimer.Create(() => {
            Setup(sender as Match3, e.grid);
        }, .1f);
    }

    public void Setup(Match3 match3, Grid<Match3.GemGridPosition> grid) {
        this.match3 = match3;
        this.grid = grid;

        float cameraYOffset = 1f;
        cameraTransform.position = new Vector3(grid.GetWidth() * .5f, grid.GetHeight() * .5f + cameraYOffset, cameraTransform.position.z);

        match3.OnGemGridPositionDestroyed += Match3_OnGemGridPositionDestroyed;
        match3.OnNewGemGridSpawned += Match3_OnNewGemGridSpawned;

        // Initialize Visual
        gemGridDictionary = new Dictionary<Match3.GemGrid, GemGridVisual>();
        glassGridDictionary = new Dictionary<Match3.GemGridPosition, GlassGridVisual>();

        for (int x = 0; x < grid.GetWidth(); x++) {
            for (int y = 0; y < grid.GetHeight(); y++) {
                Match3.GemGridPosition gemGridPosition = grid.GetGridObject(x, y);
                Match3.GemGrid gemGrid = gemGridPosition.GetGemGrid();

                Vector3 position = grid.GetWorldPosition(x, y);
                position = new Vector3(position.x, 12);

                // Visual Transform
                Transform gemGridVisualTransform = Instantiate(pfGemGridVisual, position, Quaternion.identity);
                // Debug.Log($"Instantiated gemGridVisual at {position}, scale: {gemGridVisualTransform.localScale}");
                gemGridVisualTransform.Find("sprite").GetComponent<SpriteRenderer>().sprite = gemGrid.GetGem().sprite;

                GemGridVisual gemGridVisual = new GemGridVisual(gemGridVisualTransform, gemGrid);

                gemGridDictionary[gemGrid] = gemGridVisual;

                // Glass Visual Transform
                Transform glassGridVisualTransform = Instantiate(pfGlassGridVisual, grid.GetWorldPosition(x, y), Quaternion.identity);

                GlassGridVisual glassGridVisual = new GlassGridVisual(glassGridVisualTransform, gemGridPosition);

                glassGridDictionary[gemGridPosition] = glassGridVisual;

                // Background Grid Visual
                Instantiate(pfBackgroundGridVisual, grid.GetWorldPosition(x, y), Quaternion.identity);
            }
        }

        SetBusyState(.5f, () => SetState(State.TryFindMatches));

        isSetup = true;
    }

    private void Match3_OnNewGemGridSpawned(object sender, Match3.OnNewGemGridSpawnedEventArgs e) {
        Vector3 position = e.gemGridPosition.GetWorldPosition();
        position = new Vector3(position.x, 12);

        Transform gemGridVisualTransform = Instantiate(pfGemGridVisual, position, Quaternion.identity);
        // Debug.Log($"Instantiated gemGridVisual at {position}, scale: {gemGridVisualTransform.localScale}");
        gemGridVisualTransform.Find("sprite").GetComponent<SpriteRenderer>().sprite = e.gemGrid.GetGem().sprite;

        GemGridVisual gemGridVisual = new GemGridVisual(gemGridVisualTransform, e.gemGrid);

        gemGridDictionary[e.gemGrid] = gemGridVisual;
    }

    private void Match3_OnGemGridPositionDestroyed(object sender, System.EventArgs e) {
        Match3.GemGridPosition gemGridPosition = sender as Match3.GemGridPosition;
        
        if (gemGridPosition != null && gemGridPosition.GetGemGrid() != null) {
            Match3.GemGrid gemGrid = gemGridPosition.GetGemGrid();
            if (gemGridDictionary.ContainsKey(gemGrid)) {
                Destroy(gemGridDictionary[gemGrid].transform.gameObject);
                gemGridDictionary.Remove(gemGrid);
                // Debug.Log("Destroyed and removed GemGridVisual.");
            }
        }
    }

    private void Update() {
        if (!isSetup) return;

        UpdateVisual();

        switch (state) {
            case State.Busy:
                busyTimer -= Time.deltaTime;
                if (busyTimer <= 0f) {
                    onBusyTimerElapsedAction();
                }
                break;
            case State.WaitingForUser:
                Debug.Log("Waiting");
                if (Input.GetMouseButtonDown(0)) {
                    Vector3 mouseWorldPosition = UtilsClass.GetMouseWorldPosition();
                    grid.GetXY(mouseWorldPosition, out startDragX, out startDragY);

                    startDragMouseWorldPosition = mouseWorldPosition;
                }

                if (Input.GetMouseButtonUp(0)) {
                    Vector3 mouseWorldPosition = UtilsClass.GetMouseWorldPosition();
                    grid.GetXY(mouseWorldPosition, out int x, out int y);

                    if (x != startDragX) {
                        // Different X
                        y = startDragY;

                        if (x < startDragX) {
                            x = startDragX - 1;
                        } else {
                            x = startDragX + 1;
                        }
                    } else {
                        // Different Y
                        x = startDragX;

                        if (y < startDragY) {
                            y = startDragY - 1;
                        } else {
                            y = startDragY + 1;
                        }
                    }

                    if (match3.CanSwapGridPositions(startDragX, startDragY, x, y)) {
                        Debug.Log("updating");
                        SwapGridPositions(startDragX, startDragY, x, y);
                    }
                }
                break;
            case State.TryFindMatches:
                Debug.Log("Findingmatches");
                if (match3.TryFindMatchesAndDestroyThem()) {
                    SetBusyState(.3f, () => {
                        match3.FallGemsIntoEmptyPositions();

                        SetBusyState(.3f, () => {
                            match3.SpawnNewMissingGridPositions();

                            SetBusyState(.5f, () => SetState(State.TryFindMatches));
                        });
                    });
                } else {
                    TrySetStateWaitingForUser();
                }
                break;
            case State.GameOver:
                Debug.Log("Gameover");
                break;
        }
    }

    private void UpdateVisual() {
        List<Match3.GemGrid> keysToRemove = new List<Match3.GemGrid>();

        foreach (Match3.GemGrid gemGrid in gemGridDictionary.Keys) {
            if (gemGrid == null || gemGridDictionary[gemGrid].transform == null) {
                keysToRemove.Add(gemGrid);
            } else {
                gemGridDictionary[gemGrid].Update();
            }
        }

        // Remove destroyed objects
        foreach (Match3.GemGrid gemGrid in keysToRemove) {
            gemGridDictionary.Remove(gemGrid);
            // Debug.Log("Removed destroyed GemGridVisual from dictionary.");
        }
        }

        public void SwapGridPositions(int startX, int startY, int endX, int endY) {
            match3.SwapGridPositions(startX, startY, endX, endY);
            match3.UseMove();

            SetBusyState(.5f, () => {
                Debug.Log("Now setting state to TryFindMatches");
                SetState(State.TryFindMatches);
            });
    }

    private void SetBusyState(float busyTimer, Action onBusyTimerElapsedAction) {
        SetState(State.Busy);
        this.busyTimer = busyTimer;
        this.onBusyTimerElapsedAction = onBusyTimerElapsedAction;
    }

    private void TrySetStateWaitingForUser() {
        if (match3.TryIsGameOver()) {
            // Game Over!
            Debug.Log("Game Over!");
            SetState(State.GameOver);
        } else {
            // Keep Playing
            SetState(State.WaitingForUser);
            OnStateWaitingForUser?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetState(State state) {
        this.state = state;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public State GetState() {
        return state;
    }

    public class GemGridVisual {

        public Transform transform;
        private Match3.GemGrid gemGrid;

        public GemGridVisual(Transform transform, Match3.GemGrid gemGrid) {
            this.transform = transform;
            this.gemGrid = gemGrid;

            gemGrid.OnDestroyed += GemGrid_OnDestroyed;
        }

        private void GemGrid_OnDestroyed(object sender, System.EventArgs e) {
            transform.GetComponent<Animation>().Play();
            Destroy(transform.gameObject, 1f);
        }

        public void Update() {
            if (gemGrid == null || transform == null) {
                Debug.LogWarning("GemGridVisual: Skipping update because transform or gemGrid is null.");
                return;
            }
            Vector3 targetPosition = gemGrid.GetWorldPosition();
            Vector3 moveDir = (targetPosition - transform.position);
            // float moveSpeed = 5f; // 10f
            transform.position = targetPosition;
        }
    }

    public class GlassGridVisual {

        private Transform transform;
        private Match3.GemGridPosition gemGridPosition;

        public GlassGridVisual(Transform transform, Match3.GemGridPosition gemGridPosition) {
            this.transform = transform;
            this.gemGridPosition = gemGridPosition;

            transform.gameObject.SetActive(gemGridPosition.HasGlass());

            gemGridPosition.OnGlassDestroyed += GemGridPosition_OnGlassDestroyed;
        }

        private void GemGridPosition_OnGlassDestroyed(object sender, EventArgs e) {
            transform.gameObject.SetActive(gemGridPosition.HasGlass());
        }
    }

}
