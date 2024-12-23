namespace Gomoku
{
    /// <summary>
    /// 表示搜索树中的一个节点。
    /// </summary>
    public class Node
    {
        public Node? Parent { get; set; } // 父节点
        public Board ChessBoard { get; private set; } // 当前棋盘状态
        public int Color { get; private set; } // 当前落子的颜色
        public (int Row, int Col) Move { get; private set; } // 当前节点的落子

        public int Visits { get; set; } = 0; // 访问次数
        public int Value { get; set; } = 0; // 节点的值
        public int Depth { get; set; } = 1; // 节点的深度
        public bool IsBiggerExpanded { get; private set; } = false; // 是否进行了更大范围的扩展
        public List<Node> Children { get; private set; } = new(); // 子节点列表

        public Node(Node? parent, Board chessBoard, int color, (int Row, int Col) move)
        {
            Parent = parent;
            ChessBoard = chessBoard;
            Color = color;
            Move = move;
        }

        /// <summary>
        /// 扩展当前节点。
        /// </summary>
        public void Expand()
        {
            var locations = LocationSearch.GetKeyLocations(ChessBoard, 1);
            foreach (var loc in locations)
            {
                var newChessBoard = new Board(ChessBoard.Moves);
                newChessBoard.PlaceStone(loc);

                var child = new Node(this, newChessBoard, -newChessBoard.CurrentPlayer, loc);
                Children.Add(child);
            }
        }

        /// <summary>
        /// 更大范围的扩展。
        /// </summary>
        public void BiggerExpand()
        {
            var locations = LocationSearch.GetKeyLocations(ChessBoard, 2);
            var childrenMoves = Children.Select(child => child.Move);

            foreach (var loc in locations)
            {
                if (!childrenMoves.Contains(loc))
                {
                    var newChessBoard = new Board(ChessBoard.Moves);
                    newChessBoard.PlaceStone(loc);

                    var child = new Node(this, newChessBoard, -newChessBoard.CurrentPlayer, loc);
                    Children.Add(child);
                }
            }
            IsBiggerExpanded = true;
        }

        public override bool Equals(object? obj)
        {
            if (obj is Node other)
            {
                return Move == other.Move;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Move.GetHashCode();
        }
    }

    /// <summary>
    /// 表示蒙特卡罗树搜索（MCTS）算法的实现。
    /// </summary>
    public class MCTS
    {
        public Node Root { get; private set; } // 搜索树的根节点
        private Node CurrentNode; // 当前节点

        public MCTS(Board chessBoard)
        {
            Root = new Node(null, chessBoard, -chessBoard.CurrentPlayer, default);
            CurrentNode = Root;
        }

        /// <summary>
        /// 更新根节点。
        /// </summary>
        /// <param name="move">新的根节点对应的落子。</param>
        public void UpdateRoot((int Row, int Col) move)
        {
            if (Root.Children.Count > 0)
            {
                foreach (var child in Root.Children)
                {
                    if (child.Move == move)
                    {
                        Root = child;
                        Root.Parent = null;
                        return;
                    }
                }
            }

            var newChessBoard = new Board(Root.ChessBoard.Moves);
            newChessBoard.PlaceStone(move);
            Root = new Node(null, newChessBoard, -newChessBoard.CurrentPlayer, move);
        }

        /// <summary>
        /// 进行一次完整的搜索过程。
        /// </summary>
        public void Search()
        {
            foreach (var _ in SearchInternal(CancellationToken.None, false, 1000 + (Root.Children.Any() ? Root.Children.MaxBy(child => child.Visits)!.Visits : 0))) { }
        }

        /// <summary>
        /// 进行可中断的搜索。
        /// </summary>
        /// <param name="token">取消搜索的令牌。</param>
        /// <returns>搜索过程中生成的节点。</returns>
        public IEnumerable<Node> Search(CancellationToken token)
        {
            return SearchInternal(token, true, null);
        }

        private IEnumerable<Node> SearchInternal(CancellationToken token, bool yieldResults, int? threshold)
        {
            Root.Depth = 1;

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (yieldResults && Root.Visits % 100 == 0 && Root.Visits > 1)
                {
                    yield return Root;
                }

                if (threshold.HasValue && Root.Children.Any(child => child.Visits > threshold.Value))
                {
                    break;
                }

                CurrentNode = Root;

                // 选择子节点
                while (CurrentNode.Children.Count > 0)
                {
                    if (ShouldExpandBigger(CurrentNode))
                    {
                        CurrentNode.BiggerExpand();
                    }

                    CurrentNode = SelectChild(CurrentNode);
                    UpdateDepth(CurrentNode);
                }

                // 游戏结束节点处理
                if (CurrentNode.ChessBoard.IsGameOver())
                {
                    int value = EvaluateGameEnd(CurrentNode);
                    BackPropagate(value);
                    continue;
                }

                // 扩展节点
                if (ShouldExpand(CurrentNode))
                {
                    CurrentNode.Expand();
                    CurrentNode = CurrentNode.Children[0];
                }

                // Rollout 和回溯
                int rolloutValue = Rollout();
                BackPropagate(rolloutValue);
            }
        }

        private bool ShouldExpandBigger(Node node)
        {
            return node.Depth <= 2 && !node.IsBiggerExpanded && Root.ChessBoard.Moves.Count >= 8;
        }

        private bool ShouldExpand(Node node)
        {
            return node.Visits != 0 || node.Parent == null;
        }

        private Node SelectChild(Node node)
        {
            return node.Children.MaxBy(CalculateUCB)!;
        }

        private void UpdateDepth(Node node)
        {
            node.Depth = node.Parent!.Depth + 1;
        }

        private int EvaluateGameEnd(Node node)
        {
            if (node.ChessBoard.Winner == node.Color)
            {
                return 1;
            }
            else if (node.ChessBoard.Winner == -node.Color)
            {
                return -1;
            }
            return 0;
        }

        public Node SelectBestChild()
        {
            var bestChild = Root.Children.MaxBy(CalculateWinRate)!;
            Root = bestChild;
            Root.Parent = null;

            return bestChild;
        }

        private static double CalculateUCB(Node node)
        {
            return CalculateWinRate(node) + Math.Pow(node.Parent!.Visits + 0.01, 0.25) / (node.Visits + 0.01);
        }

        public static double CalculateWinRate(Node node)
        {
            return node.Value / (2.0 * (node.Visits + 0.01)) + 0.5;
        }

        private int Rollout()
        {
            var chessBoard = new Board(CurrentNode.ChessBoard.Moves);
            var vacancies = LocationSearch.GetVacancies(chessBoard, 1);
            if (!chessBoard.IsGameOver())
            {
                for (int i = 0; i < 20; i++)
                {
                    var loc = LocationSearch.RandomMove(chessBoard, vacancies);
                    chessBoard.PlaceStone(loc);
                    if (chessBoard.IsGameOver())
                    {
                        break;
                    }

                    var (row, col) = loc;
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        if (row + dr < 0 || row + dr >= chessBoard.Size) continue;

                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (col + dc < 0 || col + dc >= chessBoard.Size) continue;

                            if (chessBoard.IsLegalMove((row + dr, col + dc)))
                            {
                                vacancies.Add((row + dr, col + dc));
                            }
                        }
                    }
                    vacancies.Remove(loc);
                }
            }

            if (chessBoard.Winner == 0)
            {
                return 0;
            }
            else if (chessBoard.Winner == CurrentNode.Color)
            {
                return 1;
            }
            else if (chessBoard.Winner == -CurrentNode.Color)
            {
                return -1;
            }
            return 0;
        }

        private void BackPropagate(int value)
        {
            while (CurrentNode.Parent != null)
            {
                CurrentNode.Visits++;
                CurrentNode.Value += value;
                CurrentNode = CurrentNode.Parent;
                value *= -1;
            }

            Root.Visits++;
        }
    }
}
