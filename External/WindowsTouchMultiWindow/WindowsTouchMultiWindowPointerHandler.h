/*
* @author Jorrit de Vries (jorrit@ijsfontein.nl)
*/

#pragma once

#include <string>
#include <sstream>

#include "WindowsTouchMultiWindow.h"
#include "WindowsTouchMultiWindowCommon.h"

class EXPORT_API PointerHandler
{
private:
	int mTargetDisplay;
	TOUCH_API mApi;
	HWND mHWnd;
	HINSTANCE mHInstance;
	LONG_PTR mPreviousWndProc;
	GET_POINTER_INFO mGetPointerInfo;
	GET_POINTER_TOUCH_INFO mGetPointerTouchInfo;
	GET_POINTER_PEN_INFO mGetPointerPenInfo;
	PointerCallback mPointerCallback;

	int mWidth;
	int mHeight;

	float mOffsetX;
	float mOffsetY;

	float mScaleX;
	float mScaleY;

	bool mEnableMouse;
	bool mEnableMouseInPointer;
public:
	/**	*/
	PointerHandler();
	/**	*/
	~PointerHandler();

	/// <summary>
	/// Initializes the plugin
	/// </summary>
	/// <param name="messageCallback"></param>
	/// <param name="targetDisplay"></param>
	/// <param name="api"></param>
	/// <param name="hWnd"></param>
	/// <param name="pointerCallback"></param>
	/// <returns></returns>
	Result initialize(MessageCallback messageCallback, int targetDisplay, TOUCH_API api, HWND hWnd, PointerCallback pointerCallback);

	/// <summary>
	/// Gets the target display
	/// </summary>
	/// <returns></returns>
	int getTargetDisplay() const;

	/// <summary>
	/// Sets the target display into
	/// </summary>
	/// <param name="messageCallback"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	Result setTargetDisplay(MessageCallback messageCallback, int value);

	/// <summary>
	/// Sets parameters for target display
	/// </summary>
	/// <param name="messageCallback"></param>
	/// <param name="width"></param>
	/// <param name="height"></param>
	/// <param name="offsetX"></param>
	/// <param name="offsetY"></param>
	/// <param name="scaleX"></param>
	/// <param name="scaleY"></param>
	/// <returns></returns>
	Result setDisplayParams(MessageCallback messageCallback, int width, int height, float offsetX, float offsetY, float scaleX, float scaleY);

	/// <summary>
	/// Sets parameters to handle mouse events
	/// </summary>
	/// <param name="messageCallback"></param>
	/// <param name="enableMouse"></param>
	/// <param name="enableMouseInPointer"></param>
	/// <returns></returns>
	Result setMouseParams(MessageCallback messageCallback, bool enableMouse, bool enableMouseInPointer);
private:
	/**	*/
	void sendMessage(MessageCallback messageCallback, MessageType messageType, const std::string& message);
	/**	*/
	bool decodeWin8Touches(UINT msg, WPARAM wParam, LPARAM lParam);
	/**	*/
	void decodeWin7Touches(UINT msg, WPARAM wParam, LPARAM lParam);

	/// <summary>
	/// Delegate to handle Window messages on Windows 8+
	/// </summary>
	/// <param name="hwnd"></param>
	/// <param name="msg"></param>
	/// <param name="wParam"></param>
	/// <param name="lParam"></param>
	/// <returns></returns>
	static LRESULT CALLBACK wndProc8(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
	
	/// <summary>
	/// Delegate to handle Window messages on Windows 7
	/// </summary>
	/// <param name="hwnd"></param>
	/// <param name="msg"></param>
	/// <param name="wParam"></param>
	/// <param name="lParam"></param>
	/// <returns></returns>
	static LRESULT CALLBACK wndProc7(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
};
