using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Gomoku;

namespace GomokuAI
{
    public partial class MainWindow : Window
    {
        private Board gameBoard;
        private MCTS monteCarloTreeSearch;
        private CancellationTokenSource? cancellationTokenSource;

        private const int CellSize = 50;
        private const int BoardSize = 15;
        private const int StoneSize = 40;
        private const int IndicatorSize = 10;

        private Ellipse moveIndicator = new();
        private List<UIElement> searchResults = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeChessboard();
            gameBoard = new();
            monteCarloTreeSearch = new(new());
        }

        /// <summary>
        /// 绘制棋盘
        /// </summary>
        private void InitializeChessboard()
        {
            chessBoardCanvas.Children.Clear();
            for (int i = 0; i < BoardSize; i++)
            {
                DrawHorizontalLine(i);
                DrawVerticalLine(i);
                AddColumnNumberLabel(i);
                AddRowNumberLabel(i);
            }
        }

        /// <summary>
        /// 绘制水平线
        /// </summary>
        private void DrawHorizontalLine(int rowIndex)
        {
            Line line = new()
            {
                X1 = CellSize,
                Y1 = (rowIndex + 1) * CellSize,
                X2 = BoardSize * CellSize,
                Y2 = (rowIndex + 1) * CellSize,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                IsEnabled = false
            };
            chessBoardCanvas.Children.Add(line);
        }

        /// <summary>
        /// 绘制垂直线
        /// </summary>
        private void DrawVerticalLine(int columnIndex)
        {
            Line line = new()
            {
                X1 = (columnIndex + 1) * CellSize,
                Y1 = CellSize,
                X2 = (columnIndex + 1) * CellSize,
                Y2 = BoardSize * CellSize,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                IsEnabled = false
            };
            chessBoardCanvas.Children.Add(line);
        }

        /// <summary>
        /// 绘制列号
        /// </summary>
        private void AddColumnNumberLabel(int columnIndex)
        {
            TextBlock label = new()
            {
                Text = columnIndex.ToString(),
                FontSize = 16,
                Width = 32,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
            };
            Canvas.SetLeft(label, columnIndex * CellSize + 35);
            Canvas.SetTop(label, 30);
            chessBoardCanvas.Children.Add(label);
        }

        /// <summary>
        /// 绘制行号
        /// </summary>
        private void AddRowNumberLabel(int rowIndex)
        {
            TextBlock label = new()
            {
                Text = rowIndex.ToString(),
                FontSize = 16,
                Width = 32,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Right,
            };
            Canvas.SetLeft(label, 10);
            Canvas.SetTop(label, rowIndex * CellSize + 40);
            chessBoardCanvas.Children.Add(label);
        }

        /// <summary>
        /// 重置游戏状态
        /// </summary>
        private void ResetGame(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
            gameBoard = new Board();
            monteCarloTreeSearch = new(new());
            InitializeChessboard();
            DropBtn.IsEnabled = true;
        }

        /// <summary>
        /// 落子
        /// </summary>
        private void PlaceStone((int, int) move)
        {
            if (gameBoard.IsLegalMove(move))
            {
                Dispatcher.Invoke(() => DrawStone(move, gameBoard.CurrentPlayer == 1));
                Dispatcher.Invoke(() => UpdateMoveIndicator(move));
                gameBoard.PlaceStone(move);
            }
        }

        /// <summary>
        /// 处理鼠标点击落子
        /// </summary>
        private async void PlaceStoneOnBoard(object sender, MouseButtonEventArgs e)
        {
            if (gameBoard.IsGameOver())
            {
                return;
            }

            Point position = e.GetPosition(chessBoardCanvas);
            if (position.X < 25 || position.Y < 25 || position.X > 775 || position.Y > 775)
            {
                return;
            }

            int columnIndex = (int)((position.X - 25) / CellSize);
            int rowIndex = (int)((position.Y - 25) / CellSize);
            if (!gameBoard.IsLegalMove((columnIndex, rowIndex)))
            {
                return;
            }

            cancellationTokenSource?.Cancel();
            PlaceStone((columnIndex, rowIndex));

            if (!gameBoard.IsGameOver())
            {
                monteCarloTreeSearch.UpdateRoot((columnIndex, rowIndex));
                await PerformMonteCarloSimulation(columnIndex, rowIndex);
            }

            DropBtn.IsEnabled = false;
        }

        /// <summary>
        /// 按棋谱落子
        /// </summary>
        private async void PlaceStonesFromNotation(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(NotationTextBox.Text))
            {
                return;
            }

            JsonArray notation = JsonNode.Parse(NotationTextBox.Text)!.AsArray();
            foreach (var move in notation)
            {
                PlaceStone(((int)move![0]!, (int)move![1]!));
                monteCarloTreeSearch.UpdateRoot(((int)move![0]!, (int)move![1]!));
            }

            if (!gameBoard.IsGameOver())
            {
                await PerformMonteCarloSimulation();
            }

            DropBtn.IsEnabled = false;
        }

        /// <summary>
        /// 展示蒙特卡洛树搜索的结果
        /// </summary>
        private void DisplaySearchResults(List<Node> nodes)
        {
            ClearPreviousResults();

            // Sort nodes by win rate in descending order
            nodes.Sort((a, b) => MCTS.CalculateWinRate(b).CompareTo(MCTS.CalculateWinRate(a)));

            // Draw each node result
            for (int i = 0; i < nodes.Count; i++)
            {
                DrawNodeResult(nodes[i], i == 0);
            }
        }

        /// <summary>
        /// 绘制单个节点的结果
        /// </summary>
        private void DrawNodeResult(Node node, bool isTopResult)
        {
            Color color = isTopResult ? Colors.Red : Colors.Orange;

            // Draw the node circle
            Ellipse nodeCircle = new()
            {
                Width = StoneSize,
                Height = StoneSize,
                Fill = new SolidColorBrush(color),
            };
            SetElementPosition(nodeCircle, node.Move.Row, node.Move.Col, StoneSize);
            searchResults.Add(nodeCircle);
            chessBoardCanvas.Children.Add(nodeCircle);

            // Draw the node information text
            TextBlock nodeInfo = new()
            {
                Text = $"{(node.Visits / 1000.0):F1}k\n{MCTS.CalculateWinRate(node) * 100:F1}%",
                FontSize = 12,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
            };
            SetElementPosition(nodeInfo, node.Move.Row, node.Move.Col, 30);
            searchResults.Add(nodeInfo);
            chessBoardCanvas.Children.Add(nodeInfo);
        }

        /// <summary>
        /// 绘制棋子
        /// </summary>
        private void DrawStone((int, int) move, bool isBlack)
        {
            Ellipse stone = new()
            {
                Width = StoneSize,
                Height = StoneSize,
                Fill = isBlack ? CreateBlackStoneBrush() : CreateWhiteStoneBrush()
            };
            SetElementPosition(stone, move.Item1, move.Item2, StoneSize);
            chessBoardCanvas.Children.Add(stone);
        }

        /// <summary>
        /// 更新指示器
        /// </summary>
        private void UpdateMoveIndicator((int, int) move)
        {
            chessBoardCanvas.Children.Remove(moveIndicator);

            moveIndicator = new()
            {
                Width = IndicatorSize,
                Height = IndicatorSize,
                Fill = new SolidColorBrush(Colors.Blue),
            };
            SetElementPosition(moveIndicator, move.Item1, move.Item2, IndicatorSize);
            chessBoardCanvas.Children.Add(moveIndicator);
        }

        /// <summary>
        /// 清楚上一次搜索结果
        /// </summary>
        private void ClearPreviousResults()
        {
            foreach (var result in searchResults)
            {
                chessBoardCanvas.Children.Remove(result);
            }
            searchResults.Clear();
        }

        /// <summary>
        /// 设置UI元素的位置
        /// </summary>
        private void SetElementPosition(UIElement element, int columnIndex, int rowIndex, int size)
        {
            Canvas.SetLeft(element, (columnIndex + 1) * CellSize - size / 2.0);
            Canvas.SetTop(element, (rowIndex + 1) * CellSize - size / 2.0);
        }

        /// <summary>
        /// 进行蒙特卡洛树搜索
        /// </summary>
        private async Task PerformMonteCarloSimulation(int columnIndex = -1, int rowIndex = -1)
        {
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            await Task.Run(() =>
            {
                foreach (var root in monteCarloTreeSearch.Search(token))
                {
                    Dispatcher.Invoke(() => DisplaySearchResults(root.Children));
                }
            }, token);
        }

        /// <summary>
        /// 黑棋笔刷
        /// </summary>
        private static RadialGradientBrush CreateBlackStoneBrush()
        {
            return new RadialGradientBrush()
            {
                GradientStops =
                [
                    new GradientStop(Colors.White, 0.0),
                    new GradientStop(Colors.LightGray, 0.2),
                    new GradientStop(Colors.Black, 1.0)
                ],
                Center = new Point(0.3, 0.3),
                GradientOrigin = new Point(0.3, 0.3),
                RadiusX = 0.4,
                RadiusY = 0.4
            };
        }

        /// <summary>
        /// 白棋笔刷
        /// </summary>
        private static RadialGradientBrush CreateWhiteStoneBrush()
        {
            return new RadialGradientBrush()
            {
                GradientStops =
                [
                    new GradientStop(Colors.White, 0.0),
                    new GradientStop(Colors.WhiteSmoke, 0.2),
                    new GradientStop(Colors.LightGray, 1.0)
                ],
                Center = new Point(0.3, 0.3),
                GradientOrigin = new Point(0.3, 0.3),
                RadiusX = 0.4,
                RadiusY = 0.4
            };
        }
    }
}