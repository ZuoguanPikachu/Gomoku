namespace Gomoku
{
    /// <summary>
    /// 表示五子棋棋盘。
    /// </summary>
    public class Board
    {
        private const int WinLength = 5; // 获胜所需的连续棋子数
        public readonly int Size = 15; // 棋盘的大小（15x15）
        public int[,] Grid { get; private set; } = new int[15, 15]; // 用于表示棋盘的二维数组
        public List<(int Row, int Col)> Moves { get; private set; } = new(); // 已下的棋子位置列表
        public int CurrentPlayer { get; private set; } = 1; // 当前轮到的玩家（1 或 -1）
        public int Winner { get; private set; } = 0; // 获胜者（1、-1 或 0 表示无获胜者）

        /// <summary>
        /// 初始化一个空棋盘。
        /// </summary>
        public Board() { }

        /// <summary>
        /// 使用给定的落子列表初始化棋盘。
        /// </summary>
        /// <param name="moves">用于初始化棋盘的落子列表。</param>
        public Board(List<(int Row, int Col)> moves)
        {
            foreach (var move in moves)
            {
                PlaceStone(move);
            }
        }

        /// <summary>
        /// 检查某一步落子是否合法（即该位置为空）。
        /// </summary>
        /// <param name="move">要检查的落子位置。</param>
        /// <returns>如果合法返回 true，否则返回 false。</returns>
        public bool IsLegalMove((int Row, int Col) move)
        {
            return Grid[move.Row, move.Col] == 0;
        }

        /// <summary>
        /// 在棋盘上落下一颗棋子，并切换当前玩家。
        /// </summary>
        /// <param name="move">要落子的坐标。</param>
        public void PlaceStone((int Row, int Col) move)
        {
            if (!IsLegalMove(move)) throw new InvalidOperationException("非法落子。");

            Grid[move.Row, move.Col] = CurrentPlayer;
            Moves.Add(move);
            CurrentPlayer = -CurrentPlayer;
        }

        /// <summary>
        /// 检查游戏是否结束（获胜或平局）。
        /// </summary>
        /// <returns>如果游戏结束返回 true，否则返回 false。</returns>
        public bool IsGameOver()
        {
            if (Moves.Count == 0)
            {
                return false;
            }

            var lastMove = Moves[^1];
            var lastPlayer = -CurrentPlayer; // 上一轮下棋的玩家
            int[] directionsRow = { 1, 0, 1, 1 }; // 检测方向的行偏移
            int[] directionsCol = { 0, 1, 1, -1 }; // 检测方向的列偏移

            foreach (var dir in Enumerable.Range(0, 4))
            {
                if (CheckLine(lastMove, lastPlayer, directionsRow[dir], directionsCol[dir]))
                {
                    Winner = lastPlayer;
                    return true;
                }
            }

            // 检查是否平局
            if (Moves.Count == Size * Size)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查某一方向上是否存在连续的棋子。
        /// </summary>
        /// <param name="start">起始位置。</param>
        /// <param name="player">需要检查的玩家。</param>
        /// <param name="dRow">行方向的偏移量。</param>
        /// <param name="dCol">列方向的偏移量。</param>
        /// <returns>如果找到连续的棋子返回 true，否则返回 false。</returns>
        private bool CheckLine((int Row, int Col) start, int player, int dRow, int dCol)
        {
            int count = 1; // 计数当前起始位置的棋子

            // 检查正方向
            count += CountStonesInDirection(start, player, dRow, dCol);

            // 检查反方向
            count += CountStonesInDirection(start, player, -dRow, -dCol);

            return count >= WinLength;
        }

        /// <summary>
        /// 统计指定方向上连续的棋子数量。
        /// </summary>
        /// <param name="start">起始位置。</param>
        /// <param name="player">需要检查的玩家。</param>
        /// <param name="dRow">行方向的偏移量。</param>
        /// <param name="dCol">列方向的偏移量。</param>
        /// <returns>找到的连续棋子数。</returns>
        private int CountStonesInDirection((int Row, int Col) start, int player, int dRow, int dCol)
        {
            int count = 0;
            int row = start.Row + dRow;
            int col = start.Col + dCol;

            while (row >= 0 && row < Size && col >= 0 && col < Size && Grid[row, col] == player)
            {
                count++;
                row += dRow;
                col += dCol;
            }

            return count;
        }
    }
}
