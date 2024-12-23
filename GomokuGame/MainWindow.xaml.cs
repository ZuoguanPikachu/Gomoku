using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Gomoku;

namespace GomokuGame
{
    public partial class MainWindow : Window
    {
        private bool gameStarted = false;
        private bool isPlayerRound = false;
        private Board gameBoard = new();
        private MCTS agent;

        private const int CellSize = 50;
        private const int BoardSize = 15;
        private const int StoneSize = 40;
        private const int IndicatorSize = 10;

        private Ellipse moveIndicator = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeChessboard();
            agent = new MCTS(gameBoard);
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
        /// 游戏开始按钮点击事件
        /// </summary>
        private void Start(object sender, RoutedEventArgs e)
        {
            SetStartState();

            if (AISente.IsChecked == true)
            {
                PlayStone((7, 7)); // AI 首次落子
            }

            agent = new MCTS(new Board(gameBoard.Moves)); // 初始化AI代理
            SwitchToPlayerRound();
        }

        /// <summary>
        /// 重置游戏状态
        /// </summary>
        private void Reset(object sender, RoutedEventArgs e)
        {
            ResetBtn.IsEnabled = false;
            StartBtn.IsEnabled = true;
            isPlayerRound = false;

            StatusInfo.Text = string.Empty;
            LoadingLine.Visibility = Visibility.Collapsed;

            gameBoard = new Board();
            InitializeChessboard();
        }

        /// <summary>
        /// 玩家落子
        /// </summary>
        private void PlayStone((int, int) move)
        {
            if (gameBoard.IsLegalMove(move))
            {
                Dispatcher.Invoke(() => DrawStone(move, gameBoard.CurrentPlayer == 1));
                Dispatcher.Invoke(() => UpdateMoveIndicator(move));
                gameBoard.PlaceStone(move);
            }
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
        /// 设置UI元素的位置
        /// </summary>
        private void SetElementPosition(UIElement element, int columnIndex, int rowIndex, int size)
        {
            Canvas.SetLeft(element, (columnIndex + 1) * CellSize - size / 2.0);
            Canvas.SetTop(element, (rowIndex + 1) * CellSize - size / 2.0);
        }

        /// <summary>
        /// 玩家点击落子事件
        /// </summary>
        private void PlayerDrop(object sender, MouseButtonEventArgs e)
        {
            if (gameStarted && isPlayerRound && !gameBoard.IsGameOver())
            {
                Point position = e.GetPosition(chessBoardCanvas);
                if (position.X >= 25 && position.Y >= 25 && position.X <= 775 && position.Y <= 775)
                {
                    int i = ((int)(position.X - 25)) / 50;
                    int j = ((int)(position.Y - 25)) / 50;

                    if (gameBoard.IsLegalMove((i, j)))
                    {
                        PlayerMove((i, j)); // 进行玩家回合
                    }
                }
            }
        }

        /// <summary>
        /// 玩家进行一轮
        /// </summary>
        private async void PlayerMove((int, int) move)
        {
            PlayStone(move);
            SwitchToAIRound();

            if (gameBoard.IsGameOver())
            {
                ShowGameEnded();
            }
            else
            {
                await Task.Run(() =>
                {
                    ShowSearching();
                    DisableResetBtn();
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    agent.UpdateRoot(move);
                    agent.Search();

                    var bestChild = agent.SelectBestChild();
                    PlayStone(bestChild.Move);

                    stopwatch.Stop();
                    ShowStatusInfo(bestChild, stopwatch.ElapsedMilliseconds);
                    EnableResetBtn();
                    SwitchToPlayerRound();
                });

                if (gameBoard.IsGameOver())
                {
                    ShowGameEnded();
                }
            }
        }

        /// <summary>
        /// 切换到AI回合
        /// </summary>
        private void SwitchToAIRound()
        {
            isPlayerRound = false;
        }

        /// <summary>
        /// 切换到玩家回合
        /// </summary>
        private void SwitchToPlayerRound()
        {
            isPlayerRound = true;
        }

        private void DisableResetBtn()
        {
            Dispatcher.Invoke(() => { ResetBtn.IsEnabled = false; });
        }

        private void EnableResetBtn()
        {
            Dispatcher.Invoke(() => { ResetBtn.IsEnabled = true; });
        }

        private void ShowSearching()
        {
            Dispatcher.Invoke(() => { LoadingLine.Visibility = Visibility.Visible; });
        }

        private void ShowStatusInfo(Node currentNode, long elapsedMilliseconds)
        {
            Dispatcher.Invoke(() => {
                LoadingLine.Visibility = Visibility.Collapsed;
                double winRate = MCTS.CalculateWinRate(currentNode) * 100;
                double dt = elapsedMilliseconds / 1000.0;
                int steps = currentNode.Visits;
                StatusInfo.Text = $"{winRate:F1}%; {dt:F1}s; {steps}";
            });
        }

        private static void ShowGameEnded()
        {
            MessageBox.Show("游戏结束！");
        }

        private void SetStartState()
        {
            gameStarted = true;
            ResetBtn.IsEnabled = true;
            StartBtn.IsEnabled = false;
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
