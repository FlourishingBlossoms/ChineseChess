"""
LLM API服务 - 用于Lunatic难度AI的大语言模型调用

这是一个Flask服务，提供HTTP API接口供Unity游戏调用。
服务会接收棋盘状态，构建提示词，调用大语言模型API，返回走法建议。

【运行方式】
1. 安装依赖: pip install flask flask-cors z-ai-web-dev-sdk
2. 运行服务: python llm_api_server.py
3. 服务将在 http://localhost:5000 启动

【API接口】
POST /api/chess/analyze
请求体: {"board_state": "...", "legal_moves": [...]}
返回: {"analysis": "...", "suggested_move_index": 0}
"""

import json
import asyncio
from flask import Flask, request, jsonify
from flask_cors import CORS

# 尝试导入z-ai-web-dev-sdk
try:
    import ZAI_SDK as ZAI
    ZAI_AVAILABLE = True
    print("[LLM API] z-ai-web-dev-sdk 可用")
except ImportError:
    ZAI_AVAILABLE = False
    print("[LLM API] 警告: z-ai-web-dev-sdk 不可用，将使用模拟响应")

app = Flask(__name__)
CORS(app)  # 允许跨域请求

# ==================== 系统提示词 ====================

SYSTEM_PROMPT = """你是一个中国象棋大师AI，正在执黑方与红方对弈。

你的任务是分析当前棋局，选择最佳走法。你需要：
1. 分析当前局面的优劣势
2. 识别可能的战术机会（将军、吃子、威胁等）
3. 考虑防守需求
4. 选择最优走法并解释理由

输出格式要求：
1. 先简要分析当前局面（2-3句话）
2. 然后输出你选择的走法编号，格式为：选择：[编号]
例如：选择：[5]

请记住：
- 你执黑方，目标是击败红方
- 优先考虑将军和吃子的机会
- 注意防守，不要让将帅暴露在危险中
- 考虑长远战略，不要只看眼前利益"""


def build_user_prompt(board_state: str, legal_moves: list) -> str:
    """
    构建用户提示词
    
    Args:
        board_state: 棋盘状态描述
        legal_moves: 合法走法列表
    
    Returns:
        完整的用户提示词
    """
    prompt = f"""当前棋盘状态：
{board_state}

所有合法走法（格式：[编号] 棋子 从(行,列)到(行,列) [吃子]）：
"""
    
    for i, move in enumerate(legal_moves):
        capture_info = " [可吃子]" if move.get('can_capture') else ""
        prompt += f"[{i}] {move.get('piece_name', '棋子')} 从({move.get('from_y')},{move.get('from_x')}) 到({move.get('to_y')},{move.get('to_x')}){capture_info}\n"
    
    prompt += """
请分析局面并选择最佳走法。输出格式：
1. 简要分析（2-3句话）
2. 选择：[编号]

请开始分析："""
    
    return prompt


async def call_llm_async(prompt: str) -> str:
    """
    异步调用大语言模型API
    
    Args:
        prompt: 用户提示词
    
    Returns:
        LLM的响应文本
    """
    if not ZAI_AVAILABLE:
        # 模拟响应
        return """当前局面分析：红方棋子分布较为分散，我方应抓住机会进行进攻。车和炮的位置较好，可以形成威胁。

选择：[0]"""
    
    try:
        # 创建ZAI实例
        zai = await ZAI.create()
        
        # 调用chat completions API
        completion = await zai.chat.completions.create(
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": prompt}
            ],
            temperature=0.7,  # 适中的创造性
            max_tokens=500    # 限制响应长度
        )
        
        # 提取响应内容
        if completion and completion.choices and len(completion.choices) > 0:
            return completion.choices[0].message.content
        else:
            return "分析失败，请使用默认走法。选择：[0]"
            
    except Exception as e:
        print(f"[LLM API] 调用失败: {e}")
        return f"API调用错误: {str(e)}。选择：[0]"


def parse_move_index(response: str, max_moves: int) -> int:
    """
    从LLM响应中解析走法编号
    
    Args:
        response: LLM的响应文本
        max_moves: 最大走法数量
    
    Returns:
        走法编号（0到max_moves-1），解析失败返回0
    """
    try:
        # 方法1：查找"选择：[编号]"格式
        import re
        match = re.search(r'选择[：:]\s*\[(\d+)\]', response)
        if match:
            index = int(match.group(1))
            if 0 <= index < max_moves:
                return index
        
        # 方法2：查找单独的[编号]
        match = re.search(r'\[(\d+)\]', response)
        if match:
            index = int(match.group(1))
            if 0 <= index < max_moves:
                return index
        
        # 方法3：查找数字
        numbers = re.findall(r'\b(\d+)\b', response)
        for num_str in numbers:
            index = int(num_str)
            if 0 <= index < max_moves:
                return index
        
        # 默认返回0
        return 0
        
    except Exception as e:
        print(f"[LLM API] 解析走法失败: {e}")
        return 0


# ==================== API路由 ====================

@app.route('/api/chess/analyze', methods=['POST'])
def analyze_chess():
    """
    分析棋局并返回走法建议
    
    请求体:
    {
        "board_state": "棋盘状态描述",
        "legal_moves": [
            {"from_x": 0, "from_y": 0, "to_x": 1, "to_y": 1, "piece_name": "车", "can_capture": false},
            ...
        ]
    }
    
    返回:
    {
        "success": true,
        "analysis": "LLM的分析文本",
        "suggested_move_index": 0,
        "raw_response": "LLM的原始响应"
    }
    """
    try:
        data = request.get_json()
        
        if not data:
            return jsonify({
                "success": false,
                "error": "无效的请求数据"
            }), 400
        
        board_state = data.get('board_state', '')
        legal_moves = data.get('legal_moves', [])
        
        if not legal_moves:
            return jsonify({
                "success": false,
                "error": "没有合法走法"
            }), 400
        
        # 构建提示词
        user_prompt = build_user_prompt(board_state, legal_moves)
        
        print(f"[LLM API] 收到分析请求，合法走法数量: {len(legal_moves)}")
        
        # 调用LLM（使用asyncio运行异步函数）
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            response = loop.run_until_complete(call_llm_async(user_prompt))
        finally:
            loop.close()
        
        # 解析走法编号
        move_index = parse_move_index(response, len(legal_moves))
        
        print(f"[LLM API] LLM响应: {response[:100]}...")
        print(f"[LLM API] 建议走法编号: {move_index}")
        
        return jsonify({
            "success": True,
            "analysis": response,
            "suggested_move_index": move_index,
            "raw_response": response
        })
        
    except Exception as e:
        print(f"[LLM API] 处理请求失败: {e}")
        return jsonify({
            "success": False,
            "error": str(e),
            "suggested_move_index": 0
        }), 500


@app.route('/api/chess/health', methods=['GET'])
def health_check():
    """健康检查接口"""
    return jsonify({
        "status": "ok",
        "llm_available": ZAI_AVAILABLE
    })


# ==================== 主程序 ====================

if __name__ == '__main__':
    print("=" * 50)
    print("中国象棋 LLM API 服务")
    print("=" * 50)
    print(f"LLM SDK 状态: {'可用' if ZAI_AVAILABLE else '不可用（使用模拟响应）'}")
    print("服务地址: http://localhost:5000")
    print("API端点:")
    print("  - POST /api/chess/analyze  分析棋局")
    print("  - GET  /api/chess/health   健康检查")
    print("=" * 50)
    
    app.run(host='0.0.0.0', port=5000, debug=True)
