using UnityEngine;
using Unity.MLAgents.Integrations.Match3;
using UnityEngine.UIElements;
using Unity.MLAgents;
using System.Collections; // for IEnumerator

public class MyMl : AbstractBoard
{
    [SerializeField] private Match3 match3;
    [SerializeField] private Match3Visual match3Visual;
    
    private LevelSO levelSO;
    private Agent agent;

    private bool episodeEnded = false;


    private void Start(){
        agent = GetComponent<Agent>();

        match3 = FindFirstObjectByType<Match3>();
        match3Visual = FindFirstObjectByType<Match3Visual>();
        
        match3Visual.OnStateWaitingForUser += Match3Visual_OnStateWaitingForUser;

        match3.OnGemGridPositionDestroyed += Match3_OnGemGridPositionDestroyed;
        match3.OnGlassDestroyed += Match3_OnGlassDestroyed;
        match3.OnMoveUsed += Match3_OnMoveUsed;
        match3.OnOutOfMoves += Match3_OnOutOfMoves;
        match3.OnWin += Match3_OnWin;

    }
    
    private void Match3Visual_OnStateWaitingForUser(object sender, System.EventArgs e){
        agent.RequestDecision();
    }
    
    private void Match3_OnGemGridPositionDestroyed(object sender, System.EventArgs e){
        if (levelSO.goalType == LevelSO.GoalType.Score){
            Debug.Log($"Gem destroyed: Reward added. Current Step: {agent.StepCount}");
            agent.AddReward(1f);
        }
    }
    private void Match3_OnGlassDestroyed(object sender, System.EventArgs e){
        if (levelSO.goalType == LevelSO.GoalType.Glass){
            agent.AddReward(1f);
        }
    }

    private void Match3_OnMoveUsed(object sender, System.EventArgs e){
        if (levelSO.goalType == LevelSO.GoalType.Glass){
            agent.AddReward(-0.01f);
        }
    }
    private void Match3_OnOutOfMoves(object sender, System.EventArgs e){
        if (episodeEnded) return; // Prevent double triggering
        episodeEnded = true;

        Debug.Log("Agent Lost: Out of Moves");
        agent.AddReward(-10f);

        agent.EndEpisode();
        
        StartCoroutine(ReloadScene());
        // UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
    private void Match3_OnWin(object sender, System.EventArgs e){
        if (episodeEnded) return; // Prevent double triggering
        episodeEnded = true;

        Debug.Log("Agent Won!");
        agent.AddReward(10f); // Reward for winning

        agent.EndEpisode();

        StartCoroutine(ReloadScene());
        // UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // private void Match3_OnOutOfMoves(object sender, System.EventArgs e){
    //     agent.EndEpisode();
    //     StartCoroutine(ReloadScene());
    // }

    // private void Match3_OnWin(object sender, System.EventArgs e){
    //     agent.EndEpisode();
    //     StartCoroutine(ReloadScene());
    // }

    private IEnumerator ReloadScene(){
        Debug.LogWarning("Restarting game...");

        if (match3Visual != null) {
            Debug.Log("Resetting Match3Visual state before reload.");
            match3Visual.SetState(Match3Visual.State.Busy);
        }

        yield return new WaitForSeconds(0.5f); // Small delay

        Debug.LogWarning("Loading Scene 1 now...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }


    public override int GetCellType(int row, int col)
    {
        GemSO gemSO = match3.GetGemSO(row, col);
        return levelSO.gemList.IndexOf(gemSO);
    }

    public override int GetSpecialType(int row, int col)
    {
        return match3.HasGlass(row, col) ? 1 : 0;
    }

    public override BoardSize GetMaxBoardSize()
    {
        BoardSize boardSize = new BoardSize();
        levelSO = match3.GetLevelSO();
        boardSize.Columns = levelSO.height;
        boardSize.Rows = levelSO.width;
        boardSize.NumCellTypes = levelSO.gemList.Count;
        boardSize.NumSpecialTypes = levelSO.goalType == LevelSO.GoalType.Score ? 0 : 1;
        return boardSize;
    }

    public override bool IsMoveValid(Move m)
    {
        if (!match3.HasMoveAvailable()) return false;

        int startX = m.Column;
        int startY = m.Row;
        var moveEnd = m.OtherCell();
        int endX = moveEnd.Column;
        int endY = moveEnd.Row;
        
        if (!match3.HasGemAtPosition(startX, startY) || !match3.HasGemAtPosition(endX, endY)) {
            // Debug.Log($"AI attempted to swap an empty tile: ({startX}, {startY}) <-> ({endX}, {endY})");
            return false;
        }
        
        if (!match3.CanSwapGridPositions(startX, startY, endX, endY)) {
            // Debug.Log($"AI attempted an invalid move: ({startX}, {startY}) <-> ({endX}, {endY})");
            return false;
        }
        return true;
    }

    public override bool MakeMove(Move m)
    {
        if (match3 == null)
        {
            Debug.LogError("MakeMove: match3 is NULL!");
            return false;
        }
        if (match3Visual == null)
        {
            Debug.LogError("MakeMove: match3Visual is NULL!");
            return false;
        }
        int startX = m.Column;
        int startY = m.Row;
        var moveEnd = m.OtherCell();
        int endX = moveEnd.Column;
        int endY = moveEnd.Row;
        if (match3Visual.GetState() == Match3Visual.State.WaitingForUser) {
            Debug.Log("ML move activated");
        // Allow Match3Bot or MyMl to act
            if (IsMoveValid(m)){
                // Can do this move
                Debug.Log($"AI swap a tile: ({startX}, {startY}) <-> ({endX}, {endY})");
                match3Visual.SwapGridPositions(startX, startY, endX, endY);
                return true;
            } else{
                // Can't do this move
                return false;
            }
        } else{
            // Can't do this move
            return false;
        }

    }
}
