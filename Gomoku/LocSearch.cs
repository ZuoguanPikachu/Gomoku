namespace Gomoku
{
    /// <summary>
    /// 表示某一位置的信息。
    /// </summary>
    public class LocationInfo
    {
        public (int Row, int Col) Position { get; set; } // 位置坐标
        public List<(double Value, string Source)> Values { get; private set; } = new(); // 分数与来源列表
        public double SelfValue { get; set; } // 自己的分数
        public double OpponentValue { get; set; } // 对手的分数

        /// <summary>
        /// 计算该位置的分数。
        /// </summary>
        public void CalculateValue()
        {
            var selfWeight = 1.0;
            var opponentWeight = 1.0;

            foreach (var (value, source) in Values.OrderByDescending(item => item.Value))
            {
                if (source == "self")
                {
                    SelfValue += value * selfWeight;
                    selfWeight *= 0.1;
                }
                else
                {
                    OpponentValue += value * opponentWeight;
                    opponentWeight *= 0.1;
                }
            }
        }
    }

    /// <summary>
    /// 提供位置搜索和评估功能的静态类。
    /// </summary>
    public static class LocationSearch
    {
        /// <summary>
        /// 获取所有空位的坐标。
        /// </summary>
        /// <param name="board">当前棋盘。</param>
        /// <param name="bias">搜索的范围偏移量。</param>
        /// <returns>所有空位的集合。</returns>
        public static HashSet<(int Row, int Col)> GetVacancies(Board board, int bias)
        {
            var vacancies = new HashSet<(int Row, int Col)>();
            foreach ((int moveRow, int moveCol) in board.Moves)
            {
                for (int i = -bias; i <= bias; i++)
                {
                    if (moveRow + i < 0 || moveRow + i >= board.Size) continue;

                    for (int j = -bias; j <= bias; j++)
                    {
                        if (moveCol + j < 0 || moveCol + j >= board.Size) continue;

                        vacancies.Add((moveRow + i, moveCol + j));
                    }
                }
            }

            vacancies.ExceptWith(board.Moves);
            return vacancies;
        }

        /// <summary>
        /// 获取关键位置的评分信息。
        /// </summary>
        /// <param name="board">当前棋盘。</param>
        /// <param name="bias">搜索的范围偏移量。</param>
        /// <returns>包含位置信息的集合。</returns>
        public static IEnumerable<LocationInfo> GetKeyLocationsInfo(Board board, int bias)
        {
            var vacancies = GetVacancies(board, bias);
            return GetKeyLocationsInfo(board, vacancies);
        }

        /// <summary>
        /// 获取指定空位集合的关键位置评分信息。
        /// </summary>
        /// <param name="board">当前棋盘。</param>
        /// <param name="vacancies">空位集合。</param>
        /// <returns>包含位置信息的集合。</returns>
        public static IEnumerable<LocationInfo> GetKeyLocationsInfo(Board board, HashSet<(int Row, int Col)> vacancies)
        {
            int[][] directions = { [0, 1], [1, 0], [1, 1], [1, -1] };
            var locations = new List<LocationInfo>();

            foreach ((int row, int col) in vacancies)
            {
                var location = new LocationInfo { Position = (row, col) };
                foreach (int[] direction in directions)
                {
                    int dr = direction[0];
                    int dc = direction[1];

                    var fragment = new List<int>(capacity: 9);
                    for (int i = -4; i <= 4; i++)
                    {
                        var r = row + i * dr;
                        var c = col + i * dc;
                        if (0 <= r && r < board.Size && 0 <= c && c < board.Size)
                        {
                            fragment.Add(board.Grid[r, c]);
                        }
                    }

                    if (fragment.Count < 5) continue;

                    var (value, source) = EvaluateFragment(fragment, board.CurrentPlayer);

                    if (value > 0)
                    {
                        location.Values.Add((value, source));
                    }
                }

                if (location.Values.Any())
                {
                    location.CalculateValue();
                    locations.Add(location);
                }
            }

            if (!locations.Any())
            {
                return [new LocationInfo { Position = vacancies.First(), SelfValue = 0, OpponentValue = 0 }];
            }

            var maxOpponentValue = locations.Max(loc => loc.OpponentValue);

            if (maxOpponentValue >= 4)
            {
                return locations.Where(loc => loc.SelfValue >= 4 || loc.OpponentValue >= 4);
            }
            if (maxOpponentValue >= 3.25)
            {
                return locations.Where(loc => loc.SelfValue > 3 || loc.OpponentValue >= 3.25);
            }
            if (locations.Any(loc => loc.OpponentValue >= 2.75 && loc.OpponentValue < 3))
            {
                return locations.Where(loc => loc.SelfValue >= 2.5 || (loc.OpponentValue >= 2.75 && loc.OpponentValue < 3));
            }
            if (locations.Any(loc => loc.SelfValue >= 3.25))
            {
                return locations.Where(loc => loc.SelfValue >= 3.25 || loc.OpponentValue > 3);
            }
            if (locations.Any(loc => loc.SelfValue >= 2.75 && loc.SelfValue < 3))
            {
                return locations.Where(loc => (loc.SelfValue >= 2.75 && loc.SelfValue < 3) || loc.OpponentValue > 3);
            }
            if (maxOpponentValue >= 2.5)
            {
                return locations.Where(loc => loc.SelfValue >= 2.5 || loc.OpponentValue >= 2.5);
            }
            if (locations.Any(loc => loc.OpponentValue >= 1.5 && loc.OpponentValue < 2 && loc.SelfValue >= 1.5 && loc.SelfValue < 2))
            {
                return locations.Where(loc => loc.OpponentValue >= 1.5 && loc.SelfValue >= 1.5);
            }

            return locations.OrderByDescending(loc => loc.SelfValue + loc.OpponentValue).Take(5);
        }

        /// <summary>
        /// 获取关键位置的集合。
        /// </summary>
        /// <param name="board">当前棋盘。</param>
        /// <param name="bias">搜索范围偏移量。</param>
        /// <returns>关键位置的集合。</returns>
        public static List<(int Row, int Col)> GetKeyLocations(Board board, int bias)
        {
            return GetKeyLocationsInfo(board, bias).Select(info => info.Position).ToList();
        }

        /// <summary>
        /// 评估棋盘的一段棋子。
        /// </summary>
        /// <param name="fragment">棋子的片段。</param>
        /// <param name="currentStone">当前棋子颜色。</param>
        /// <returns>评估的分数和来源。</returns>
        public static (double Value, string Source) EvaluateFragment(List<int> fragment, int currentStone)
        {
            double maxValue = double.MinValue;
            string maxValueSource = string.Empty;

            var window = new Queue<int>();
            int selfCount = 0;
            int opponentCount = 0;

            for (int i = 0; i < fragment.Count; i++)
            {
                var stone = fragment[i];
                window.Enqueue(stone);

                if (stone == currentStone)
                {
                    selfCount++;
                }
                else if (stone == -currentStone)
                {
                    opponentCount++;
                }

                if (window.Count == 5)
                {
                    double value = double.MinValue;
                    string source = string.Empty;

                    if (opponentCount == 0)
                    {
                        value = selfCount;
                        source = "self";
                    }
                    else if (selfCount == 0)
                    {
                        value = opponentCount;
                        source = "opponent";
                    }

                    if (value == 1 && maxValue < 1.5 && source == maxValueSource)
                    {
                        value += 0.25;
                    }

                    if (value >= 2 && value == maxValue && source == maxValueSource)
                    {
                        value += 0.5;
                    }

                    if (value > maxValue)
                    {
                        maxValue = value;
                        maxValueSource = source;
                    }

                    stone = window.Dequeue();
                    if (stone == currentStone)
                    {
                        selfCount--;
                    }
                    else if (stone == -currentStone)
                    {
                        opponentCount--;
                    }
                }
            }

            return (maxValue, maxValueSource);
        }

        /// <summary>
        /// 随机选择一个可行的落子位置。
        /// </summary>
        /// <param name="board">当前棋盘。</param>
        /// <param name="vacancies">空位集合。</param>
        /// <returns>随机选择的空位。</returns>
        public static (int Row, int Col) RandomMove(Board board, HashSet<(int Row, int Col)> vacancies)
        {
            var locsInfo = GetKeyLocationsInfo(board, vacancies);

            var expSum = 0.0;
            var weigthts = new List<double>();
            foreach (var item in locsInfo)
            {
                var v = item.SelfValue + item.OpponentValue;
                weigthts.Add(v);
                expSum += Math.Exp(v);
            }

            var random = new Random();
            var r = random.NextDouble();
            var cumulativeProb = 0.0;
            for (int i = 0; i < weigthts.Count; i++)
            {
                cumulativeProb += weigthts[i] / expSum;

                if (r < cumulativeProb)
                {
                    return locsInfo.ElementAt(i).Position;
                }
            }

            return locsInfo.Last().Position;
        }
    }
}
