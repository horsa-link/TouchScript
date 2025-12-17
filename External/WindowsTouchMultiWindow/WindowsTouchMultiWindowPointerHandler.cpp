/*
* @author Jorrit de Vries (jorrit@ijsfontein.nl)
* Most is copied from WindowsTouch.cpp as authored by Valentin Simonov / http://va.lent.in/
*/

#include "WindowsTouchMultiWindowPointerHandler.h"

const wchar_t* instancePropName = L"__PointerHandler_Prop_Instance__";
MessageCallback globalMessageCallback = NULL;	// FIXME:

PointerHandler::PointerHandler()
	: mTargetDisplay(-1)
	, mApi(WIN8)
	, mHWnd(NULL)
	, mHInstance(NULL)
	, mPreviousWndProc(NULL)
	, mGetPointerInfo(NULL)
	, mGetPointerTouchInfo(NULL)
	, mGetPointerPenInfo(NULL)
	, mPointerCallback(NULL)
	, mWidth(0)
	, mHeight(0)
	, mOffsetX(0.0f)
	, mOffsetY(0.0f)
	, mScaleX(1.0f)
	, mScaleY(1.0f)
	, mEnableMouse(false)
	, mEnableMouseInPointer(false)
{

}

PointerHandler::~PointerHandler()
{
	if (mHWnd)
	{
		RemoveProp(mHWnd, instancePropName);
	}

	if (mPreviousWndProc)
	{
		SetWindowLongPtr(mHWnd, GWLP_WNDPROC, mPreviousWndProc);
		mPreviousWndProc = NULL;

		if (mApi == WIN7)
		{
			UnregisterTouchWindow(mHWnd);
		}
	}

	if (mHInstance)
	{
		FreeLibrary(mHInstance);
		mHInstance = NULL;
	}
}

Result PointerHandler::initialize(MessageCallback messageCallback, int targetDisplay, TOUCH_API api, HWND hWnd, PointerCallback pointerCallback)
{
	if (hWnd == NULL)
	{
		sendMessage(messageCallback, MT_ERROR, "hWnd is NULL");
		return R_ERROR_NULL_POINTER;
	}

	if (pointerCallback == NULL)
	{
		sendMessage(messageCallback, MT_ERROR, "pointerCallback is NULL");
		return R_ERROR_NULL_POINTER;
	}

	globalMessageCallback = messageCallback;	// FIXME:
	mTargetDisplay = targetDisplay;
	mApi = api;
	mHWnd = hWnd;
	mPointerCallback = pointerCallback;

	sendMessage(messageCallback, MT_INFO, "Initializing handler...");

	if (api == WIN8)
	{
		mHInstance = LoadLibrary(TEXT("user32.dll"));
		if (mHInstance == NULL)
		{
			sendMessage(messageCallback, MT_ERROR, "Failed to load user32.dll.");
			return R_ERROR_API;
		}

		mGetPointerInfo = (GET_POINTER_INFO)GetProcAddress(mHInstance, "GetPointerInfo");
		mGetPointerTouchInfo = (GET_POINTER_TOUCH_INFO)GetProcAddress(mHInstance, "GetPointerTouchInfo");
		mGetPointerPenInfo = (GET_POINTER_PEN_INFO)GetProcAddress(mHInstance, "GetPointerPenInfo");

		SetProp(mHWnd, instancePropName, this);
		mPreviousWndProc = SetWindowLongPtr(mHWnd, GWLP_WNDPROC, (LONG_PTR)wndProc8);

		sendMessage(messageCallback, MT_INFO, "Handler has been initialized for WIN8+.");
	}
	else
	{
		RegisterTouchWindow(mHWnd, 0);

		mPreviousWndProc = SetWindowLongPtr(mHWnd, GWLP_WNDPROC, (LONG_PTR)wndProc7);

		sendMessage(messageCallback, MT_INFO, "Handler has been initialized for WIN7.");
	}

	return R_OK;
}

int PointerHandler::getTargetDisplay() const
{
	return mTargetDisplay;
}

Result PointerHandler::setTargetDisplay(MessageCallback messageCallback, int value)
{
	sendMessage(messageCallback, MT_INFO, "Changed target display from " + std::to_string(mTargetDisplay) + " to " + std::to_string(value));

	mTargetDisplay = value;

	return R_OK;
}

Result PointerHandler::setDisplayParams(MessageCallback messageCallback, int width, int height, float offsetX, float offsetY, float scaleX, float scaleY)
{
	sendMessage(messageCallback, MT_INFO,
		"Changed size from (" + std::to_string(mWidth) + ", " + std::to_string(mHeight) + ") to (" + std::to_string(width) + ", " + std::to_string(height) + ") " +
		" and offset from (" + std::to_string(mOffsetX) + ", " + std::to_string(mOffsetY) + ") to (" + std::to_string(offsetX) + ", " + std::to_string(offsetY) + ") " +
		" and scale from (" + std::to_string(mScaleX) + ", " + std::to_string(mScaleY) + ") to (" + std::to_string(scaleX) + ", " + std::to_string(scaleY) + ")");

	mWidth = width;
	mHeight = height;
	mOffsetX = offsetX;
	mOffsetY = offsetY;
	mScaleX = scaleX;
	mScaleY = scaleY;

	return R_OK;
}

Result PointerHandler::setMouseParams(MessageCallback messageCallback, bool enableMouse, bool enableMouseInPointer)
{ 
	sendMessage(messageCallback, MT_INFO, 
		"Changed 'EnableMouse' from " + std::string(mEnableMouse ? "TRUE" : "FALSE") + " to " + std::string(enableMouse ? "TRUE" : "FALSE") +
		" and 'EnableMouseInPointer' from " + std::string(mEnableMouseInPointer ? "TRUE" : "FALSE") + " to " + std::string(enableMouseInPointer ? "TRUE" : "FALSE"));
	
	mEnableMouse = enableMouse;
	mEnableMouseInPointer = enableMouseInPointer;

	return R_OK; 
}

void PointerHandler::sendMessage(MessageCallback messageCallback, MessageType messageType, const std::string& message)
{
	if (messageCallback)
	{
		std::string st = 
			"[" + std::string(mApi == TOUCH_API::WIN8 ? "WIN8" : "WIN7") + "] " +
			"[" + std::to_string(mTargetDisplay) + "] " + 
			"[0x" + (std::ostringstream{} << std::hex << reinterpret_cast<uintptr_t>(mHWnd)).str() + "] " +
			message;

		// Allocate char array
		char* cstr = new char[st.length() + 1];
		strcpy_s(cstr, st.length() + 1, st.c_str());

		// Dispatch to callback
		messageCallback((int)messageType, cstr);

		// Unalloc char array
		delete[] cstr;
	}
}

bool PointerHandler::decodeWin8Touches(UINT msg, WPARAM wParam, LPARAM lParam)
{
	int pointerId = GET_POINTERID_WPARAM(wParam);

	POINTER_INFO pointerInfo;
	if (!mGetPointerInfo(pointerId, &pointerInfo)) return true;

	POINT p;
	p.x = pointerInfo.ptPixelLocation.x;
	p.y = pointerInfo.ptPixelLocation.y;
	ScreenToClient(mHWnd, &p);

	Vector2 position = Vector2(((float)p.x - mOffsetX) * mScaleX, mHeight - ((float)p.y - mOffsetY) * mScaleY);
	PointerData data{};
	data.pointerFlags = pointerInfo.pointerFlags;
	data.changedButtons = pointerInfo.ButtonChangeType;

	if ((pointerInfo.pointerFlags & POINTER_FLAG_CANCELED) != 0 || msg == WM_POINTERCAPTURECHANGED)
	{
		msg = POINTER_CANCELLED;
	}
	if (pointerInfo.pointerType == POINTER_INPUT_TYPE::PT_MOUSE 
		&& (!mEnableMouse || !mEnableMouseInPointer))
	{
		return false;
	}

	switch (pointerInfo.pointerType)
	{
	case PT_MOUSE:
		if ((pointerInfo.pointerFlags & POINTER_FLAG_DOWN) != 0)
		{
			sendMessage(globalMessageCallback, MT_WARNING, "POINTERCALLBACK | PT_MOUSE");
		}
		break;
	case PT_TOUCH:
		POINTER_TOUCH_INFO touchInfo;
		mGetPointerTouchInfo(pointerId, &touchInfo);
		data.flags = touchInfo.touchFlags;
		data.mask = touchInfo.touchMask;
		data.rotation = touchInfo.orientation;
		data.pressure = touchInfo.pressure;
		if ((pointerInfo.pointerFlags & POINTER_FLAG_DOWN) != 0)
		{
			sendMessage(globalMessageCallback, MT_ERROR, "POINTERCALLBACK | PT_TOUCH");
		}
		break;
	case PT_PEN:
		POINTER_PEN_INFO penInfo;
		mGetPointerPenInfo(pointerId, &penInfo);
		data.flags = penInfo.penFlags;
		data.mask = penInfo.penMask;
		data.rotation = penInfo.rotation;
		data.pressure = penInfo.pressure;
		data.tiltX = penInfo.tiltX;
		data.tiltY = penInfo.tiltY;
		break;
	}

	mPointerCallback(pointerId, msg, pointerInfo.pointerType, position, data);
	return true;
}

void PointerHandler::decodeWin7Touches(UINT msg, WPARAM wParam, LPARAM lParam)
{
	UINT cInputs = LOWORD(wParam);
	PTOUCHINPUT pInputs = new TOUCHINPUT[cInputs];

	if (!pInputs) return;
	if (!GetTouchInputInfo((HTOUCHINPUT)lParam, cInputs, pInputs, sizeof(TOUCHINPUT))) return;

	for (UINT i = 0; i < cInputs; i++)
	{
		TOUCHINPUT touch = pInputs[i];

		POINT p;
		p.x = touch.x / 100;
		p.y = touch.y / 100;
		ScreenToClient(mHWnd, &p);

		Vector2 position = Vector2(((float)p.x - mOffsetX) * mScaleX, mHeight - ((float)p.y - mOffsetY) * mScaleY);
		PointerData data{};

		if ((touch.dwFlags & TOUCHEVENTF_DOWN) != 0)
		{
			msg = WM_POINTERDOWN;
			data.changedButtons = POINTER_CHANGE_FIRSTBUTTON_DOWN;
		}
		else if ((touch.dwFlags & TOUCHEVENTF_UP) != 0)
		{
			msg = WM_POINTERLEAVE;
			data.changedButtons = POINTER_CHANGE_FIRSTBUTTON_UP;
		}
		else if ((touch.dwFlags & TOUCHEVENTF_MOVE) != 0)
		{
			msg = WM_POINTERUPDATE;
		}

		mPointerCallback(touch.dwID, msg, PT_TOUCH, position, data);
	}

	CloseTouchInputHandle((HTOUCHINPUT)lParam);
	delete[] pInputs;
}

/*
	enableMouse		enableMouseInPointer
		0					0				= return 0
		0					1				= return 0
		1					0				= return CallWindowProc
		1					1				= return PointerCallback
*/
LRESULT CALLBACK PointerHandler::wndProc8(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
	PointerHandler* handler = reinterpret_cast<PointerHandler*>(GetProp(hWnd, instancePropName));
		
	switch (msg)
	{
	case WM_TOUCH:
		handler->sendMessage(globalMessageCallback, MT_INFO, "WM_TOUCH");
		CloseTouchInputHandle((HTOUCHINPUT)lParam);
		break;
	case WM_POINTERENTER:
	case WM_POINTERLEAVE:
	case WM_POINTERDOWN:
	case WM_POINTERUP:
	case WM_POINTERUPDATE:
	case WM_POINTERCAPTURECHANGED:
		if (!handler->decodeWin8Touches(msg, wParam, lParam)	// pointer not handled and of mouse type
			&& handler->mEnableMouse 
			&& !handler->mEnableMouseInPointer)
		{
			if (msg == WM_POINTERDOWN)
			{
				handler->sendMessage(globalMessageCallback, MT_ERROR, "WNDPROC | WM_POINTERDOWN");
			}
			return CallWindowProc((WNDPROC)handler->mPreviousWndProc, hWnd, msg, wParam, lParam);
		}
		break;
	default:
		if (msg == WM_LBUTTONDOWN)
		{
			handler->sendMessage(globalMessageCallback, MT_WARNING, "WNDPROC | WM_LBUTTONDOWN");
		}
		return CallWindowProc((WNDPROC)handler->mPreviousWndProc, hWnd, msg, wParam, lParam);
	}

	return 0;
}

LRESULT CALLBACK PointerHandler::wndProc7(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
	PointerHandler* handler = reinterpret_cast<PointerHandler*>(GetProp(hWnd, instancePropName));

	switch (msg)
	{
	case WM_TOUCH:
		handler->decodeWin7Touches(msg, wParam, lParam);
		break;
	default:
		return CallWindowProc((WNDPROC)handler->mPreviousWndProc, hWnd, msg, wParam, lParam);
	}

	return 0;
}

// .NET available interface
// ----------------------------------------------------------------------------
extern "C" EXPORT_API Result PointerHandler_Create(void** handle) throw()
{
	*handle = new PointerHandler();
	return Result::R_OK;
}
extern "C" EXPORT_API Result PointerHandler_Destroy(PointerHandler* handler) throw()
{
	delete handler;
	return Result::R_OK;
}
extern "C" EXPORT_API Result PointerHandler_Initialize(PointerHandler* handler, MessageCallback messageCallback, int targetDisplay, TOUCH_API api, HWND hWnd, PointerCallback pointerCallback)
{
	return handler->initialize(messageCallback, targetDisplay, api, hWnd, pointerCallback);
}
extern "C" EXPORT_API Result PointerHandler_SetTargetDisplay(PointerHandler* handler, MessageCallback messageCallback, int targetDisplay)
{
	return handler->setTargetDisplay(messageCallback, targetDisplay);
}	
extern "C" EXPORT_API Result PointerHandler_SetDisplayParams(PointerHandler* handler, MessageCallback messageCallback, int width, int height, float offsetX, float offsetY, float scaleX, float scaleY)
{
	return handler->setDisplayParams(messageCallback, width, height, offsetX, offsetY, scaleX, scaleY);
}
extern "C" EXPORT_API Result PointerHandler_SetMouseParams(PointerHandler* handler, MessageCallback messageCallback, bool enableMouse, bool enableMouseInPointer)
{
	return handler->setMouseParams(messageCallback, enableMouse, enableMouseInPointer);
}