using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Algorithm
{
    public enum ETrendType
    {
        Up,
        Down,
        Stay
    }
    public struct AlgorithmInitParam
    {
        public int PlayerSum { get; set; }
        public int ChaoChangCoins { get; set; }
        public int StageMinRange { get; set; }
        public int StageMaxRange { get; set; }
        public int StageSpeed { get; set; }
        public int CannonMinScore { get; set; }
        public int CannonMaxScore { get; set; }
        public int ChouShuiRate { get; set; }
        public int CoinRate { get; set; }// 币值
    }
    public struct AlgorithmData
    {
        public int NormalChi{ get; set; }
        public int ExtraChi{ get; set; }
        public int TotalChi{ get{ return NormalChi + ExtraChi; } }
        public ETrendType TrendType { get; set; }
        public int CurStageProfitStart { get; set; }
        public int CurStageProfitEnd { get; set; }
        public int StageSpeed { get; set; }

        public void Init(AlgorithmInitParam param)
        {
            NormalChi = param.ChaoChangCoins;
        }

        public void Reset()
        {
            NormalChi = 0;
            ExtraChi = 0;
        }
    }
    public struct PlayerAlgData
    {
        public int PlayerId { get; set; }// 玩家ID
        public int CoinRate { get; set; }// 币值
        public long BetTotalScore { get; set; }// 该座位累计下注（单位：分）
        public long WinTotalScore { get; set; }// 该座位累计赢分（单位：分）
        public long ProfitScore { get { return BetTotalScore - WinTotalScore; } }// 该座位累计赢利（单位：分）（相对于机器来讲）
        public long BetTotalCoins { get { return BetTotalScore / CoinRate; } }// 该座位累计下注（单位：币）
        public long WinTotalCoins { get { return WinTotalScore / CoinRate; } }// 该座位累计赢分（单位：币）
        public long ProfitCoins { get { return ProfitScore / CoinRate; } }// 该座位累计赢利（单位：币）
    }
    /// <summary>
    /// 算法上下文
    /// </summary>
    public struct AlgorithmContext
    {
        public int PlayerId { get; set; }// 玩家ID
        public int PlayerScore { get; set; }// 玩家当前分值（单位：分）
        public int FishMultiple { get; set; }// 鱼倍数
        public int CannonScore { get; set; }// 炮台分值（单位：分）
    }

    public class AlgorithmDefine
    {
        public const int DefaultChaoChangCoins = 10000;
        public const int DefaultStageMinRange = 5000;
        public const int DefaultStageMaxRange = 10000;
    }
    public static class AlgorithmCenter
    {
        public static AlgorithmInitParam AlgParam { get; private set; }
        public static bool Init(AlgorithmInitParam param)
        {
            AlgParam = param;
            return true;
        }
        public static void Reset()
        {
            
        }
        public static bool IsDead(AlgorithmContext context)
        {
            return true;
        }
        private static void Save()
        {
            
        }
        private static void Load()
        {
            
        }
    }
}